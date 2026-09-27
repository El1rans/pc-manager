# OpenRGB.NET issues found while building milestone 05 (Lighting)

Two bugs found in [OpenRGB.NET](https://github.com/diogotr7/OpenRGB.NET) 3.1.1 (latest at time of
writing) while implementing PC Manager's Lighting page. Both are patched in PC Manager's vendored
copy (`src/ThirdParty/OpenRGB.NET/`, see `// PC Manager patch:` comments) rather than waiting on
upstream, but should still be reported so other users of the library benefit. The text below is
drafted so it can be filed as two upstream GitHub issues - **not yet posted**.

## Issue 1: a graceful remote close is never detected

**Summary:** When the OpenRGB SDK server closes the connection in an orderly way (the user closes
OpenRGB, or stops the SDK server from its UI), `OpenRgbClient` never notices. `Connected` keeps
reporting `true`, no event fires, and the client's background read loop spins a CPU core while
silently corrupting its own reply queue.

**Root cause:**
- `SocketExtensions.ReceiveAllAsync` treats a `0`-byte `Socket.ReceiveAsync` result (the standard
  .NET signal for "the remote end performed an orderly shutdown") as an early, silent `break` out
  of its fill loop, returning without filling the caller's buffer - and without throwing.
- `OpenRgbConnection.ReadLoop` reuses a single `headerBuffer` array across every iteration without
  clearing it. When `ReceiveAllAsync` returns early after the close, this buffer still holds
  whatever the *previous* iteration's valid header was. `ReadLoop` re-parses that stale (but
  magic-byte-valid) header and treats it as a new, real packet.
- If that stale header's command was one with a data payload, `ReadLoop` then tries to read
  `header.DataLength` more bytes - which also immediately returns 0 the same way - producing a
  zero-filled "reply" that gets enqueued into `_pendingRequests[header.Command]` as if the server
  had actually answered.
- `while (!_cancellationTokenSource.IsCancellationRequested && _socket.Connected)` never becomes
  false on its own here: nothing ever calls `Dispose()`, and `Socket.Connected` only reflects the
  state as of the *previous* I/O operation, which - because of the bug above - never actually
  observes the closed state properly propagating into that flag in every case observed. The loop
  spins continuously, pinning a CPU core and enqueueing a stream of phantom zero-filled replies
  (observed: on the order of 10^6 within seconds).
- A caller blocked on `Receive()` (e.g. a health-check calling `GetControllerCount()`) can end up
  reading one of these phantom replies and getting back a "successful" but meaningless value (e.g.
  a controller count of `0`), rather than an exception - so even a caller that explicitly checks
  for errors sees a normal-looking, if wrong, response instead of learning the connection is gone.

**Suggested fix:** in `ReceiveAllAsync`, throw (e.g. `IOException`) when `received == 0` instead of
breaking silently. This lets `ReadLoop` (with a normal `catch` around its receive calls) notice the
close, exit its loop, and mark pending requests as unfulfillable (e.g.
`BlockingCollection<T>.CompleteAdding()`) so a blocked `Receive()` call fails fast instead of ever
seeing a phantom reply.

**Repro:** connect to a real OpenRGB instance, close OpenRGB (or stop its SDK server), keep the
client's process running. `Connected` stays `true`; a background thread's CPU usage climbs to
~100% of one core; a subsequent `GetControllerCount()` (or similar) either hangs or returns a
suspicious `0`, depending on timing, instead of throwing.

## Issue 2: `DeviceListUpdated` never fires for a caller who subscribes after construction

**Summary:** Subscribing to `OpenRgbClient.DeviceListUpdated` after constructing the client (the
only time a caller *can* subscribe to it) never actually receives the event.

**Root cause:** `OpenRgbClient`'s constructor does:

```csharp
_connection = new OpenRgbConnection(DeviceListUpdated);
```

`DeviceListUpdated` here is `OpenRgbClient`'s own event, which - like any C# event - is backed by a
plain delegate field. Passing it into `OpenRgbConnection`'s constructor passes its *current value*
(always `null` at this point, since construction hasn't returned yet and nobody could have
subscribed), not a live reference to the field. `OpenRgbConnection` stores that `null` in its own
`DeviceListUpdated` property and never touches `OpenRgbClient`'s event again. Any handler a caller
adds afterwards via `client.DeviceListUpdated += ...` updates `OpenRgbClient`'s field, which
`OpenRgbConnection`'s read loop never invokes (it invokes its own, separate, permanently-null
property) - so no subscriber is ever actually notified.

**Suggested fix:** don't pass the event through the constructor at all; have `OpenRgbClient`
subscribe to `OpenRgbConnection`'s own event after constructing it:

```csharp
_connection = new OpenRgbConnection();
_connection.DeviceListUpdated += (sender, args) => DeviceListUpdated?.Invoke(sender, args);
```

**Repro:** `var client = new OpenRgbClient(...); client.DeviceListUpdated += (_, _) => Console.WriteLine("fired");`
then change the device list from another OpenRGB client (or its own UI) - nothing is printed.
