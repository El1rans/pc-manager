using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

/// <summary>
/// End-to-end tests against a real loopback socket (<see cref="FakeOpenRgbTcpServer"/>) and the
/// real vendored <c>OpenRGB.NET</c> client via <see cref="OpenRgbClientAdapter"/> - not
/// <see cref="FakeOpenRgbClient"/> - so the actual patched socket code (see
/// docs/upstream/openrgb-net.md) is what gets exercised for these two scenarios.
/// </summary>
/// <remarks>
/// These tests synchronize on explicit signals (the server accepted, the server received the
/// handshake request, the Disconnected event fired) rather than on wall-clock budgets, so they do
/// not depend on how quickly the thread pool or the OS scheduler gets around to running anything.
/// <see cref="HangGuard"/> is the only timeout left, and it is not a timing expectation.
/// </remarks>
public sealed class OpenRgbClientAdapterIntegrationTests
{
    /// <summary>Only there so a genuine hang fails the test instead of stalling the whole run -
    /// deliberately far longer than anything here takes even on a heavily loaded machine.</summary>
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);

    /// <summary>Generous on purpose: this test is about the heartbeat noticing a graceful close,
    /// which never waits for a call timeout (the patched read loop fails the pending read as soon as
    /// the socket closes). A short timeout would only let a healthy connect or heartbeat that got
    /// descheduled under load spuriously "time out" and disconnect before the test even closes the
    /// server.</summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ServerClosesGracefully_HeartbeatDetectsDisconnectWithinOneInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOpenRgbTcpServer();
        using var cts = new CancellationTokenSource();
        var acceptTask = server.AcceptAsync(cts.Token);

        var adapter = new OpenRgbClientAdapter("127.0.0.1", server.Port, timeoutMs: 2000);
        using var service = new LightingService(
            adapter,
            NullLogger<LightingService>.Instance,
            callTimeout: CallTimeout,
            heartbeatInterval: TimeSpan.FromMilliseconds(150));

        // Subscribed before connecting, so the event can never fire before anyone is listening.
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Disconnected += (_, _) => disconnected.TrySetResult();

        // Start connecting first - AcceptAsync only completes once something actually connects,
        // and nothing does until ConnectAsync's own Socket.Connect runs.
        var connectTask = service.ConnectAsync(ct);
        await acceptTask.WaitAsync(HangGuard, ct);
        var serveTask = server.ServeHandshakeThenHeartbeatsAsync(cts.Token);

        var connected = await connectTask.WaitAsync(HangGuard, ct);
        Assert.True(connected);
        Assert.False(disconnected.Task.IsCompleted, "Disconnected fired before the server closed.");

        // Simulates the user closing OpenRGB, or stopping its SDK server: an orderly TCP shutdown,
        // not a crash. This is exactly the case the vendored library's own read loop used to treat
        // as "nothing happened" (see docs/upstream/openrgb-net.md) - only the heartbeat notices.
        server.CloseClientConnectionGracefully();

        await disconnected.Task.WaitAsync(HangGuard, ct);

        Assert.False(service.IsConnected);

        await cts.CancelAsync();
        try
        {
            await serveTask.WaitAsync(HangGuard, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected: cts.CancelAsync() above.
        }
    }

    [Fact]
    public async Task ServerNeverAnswersHandshake_DisposeUnblocksThenFreshConnectSucceedsWithoutStaleSwap()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOpenRgbTcpServer();
        using var cts = new CancellationTokenSource();
        var acceptTask = server.AcceptAsync(cts.Token);

        var adapter = new OpenRgbClientAdapter("127.0.0.1", server.Port, timeoutMs: 60_000);

        // Connect() blocks a thread for as long as the handshake is unanswered, so it gets a
        // dedicated one rather than a pool thread: the test must not depend on (or starve) the
        // pool that its own awaits and the vendored read loop also need.
        // The raw TCP connect succeeds almost immediately; it's the SDK-level handshake after that
        // which this server deliberately never answers, so Connect() gets stuck there.
        var stuckConnectTask = RunOnDedicatedThread(adapter.Connect);
        await acceptTask.WaitAsync(HangGuard, ct);

        // Wait until the client has provably sent its handshake request - it is now (or is about
        // to be) blocked waiting for the reply this server will never send. Without a reply it has
        // no way to succeed, so it must not have completed.
        await server.ReadUntilHandshakeRequestAsync(cts.Token).WaitAsync(HangGuard, ct);
        Assert.False(stuckConnectTask.IsCompleted, "Connect() should still be blocked waiting for the handshake reply.");

        adapter.Dispose();

        // Dispose() must unblock the in-flight Connect() - otherwise both the socket and the thread
        // blocked inside OpenRGB.NET's handshake read would leak for good.
        var unblocked = await Task.WhenAny(stuckConnectTask, Task.Delay(HangGuard, ct)) == stuckConnectTask;
        Assert.True(unblocked, "Dispose() did not unblock the in-flight Connect().");
        Assert.False(stuckConnectTask.IsCompletedSuccessfully, "The abandoned handshake must fail, not succeed.");
        Assert.False(adapter.Connected);

        // Reconnect with the *same* adapter instance, this time letting the (same) server actually
        // answer - proves the earlier, disposed-away attempt never resurfaced and swapped itself
        // into _client once it eventually unwound.
        var acceptTask2 = server.AcceptAsync(cts.Token);
        var connectTask = RunOnDedicatedThread(adapter.Connect);
        await acceptTask2.WaitAsync(HangGuard, ct);
        var serveTask = server.ServeHandshakeThenHeartbeatsAsync(cts.Token);
        await connectTask.WaitAsync(HangGuard, ct);

        Assert.True(adapter.Connected);

        await cts.CancelAsync();
        adapter.Dispose();
        try
        {
            await serveTask.WaitAsync(HangGuard, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected: cts.CancelAsync() above.
        }
    }

    private static Task RunOnDedicatedThread(Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
