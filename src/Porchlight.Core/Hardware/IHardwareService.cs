namespace Porchlight.Core.Hardware;

/// <summary>
/// Owns every call into the hardware library on one dedicated background thread: opens it once,
/// updates roughly every second, and closes it on <see cref="Stop"/>. The UI (and
/// <c>FanControlManager</c>) only ever see the immutable <see cref="HardwareSnapshot"/>s this
/// publishes - never the underlying library's own mutable types.
/// </summary>
public interface IHardwareService
{
    /// <summary>Starts the dedicated background thread. Safe to call once; a second call is a no-op.</summary>
    void Start();

    /// <summary>Stops the background thread and releases the hardware library. Restores every
    /// controllable fan to default first (rule 5) - see also the App-level restore hooks for
    /// suspend/session-end, which do not go through <see cref="Stop"/>.</summary>
    void Stop();

    /// <summary>Raised on the dedicated background thread roughly once a second (and immediately
    /// after a re-initialization). Subscribers that touch the UI must marshal to the dispatcher
    /// themselves; <c>FanControlManager</c> deliberately handles this synchronously, on the same
    /// thread, so every <see cref="IFanController"/> call stays on the one thread that owns the
    /// hardware library.</summary>
    event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    /// <summary>The most recently published snapshot, for a consumer that starts observing after
    /// the first tick (e.g. a page navigated to later).</summary>
    HardwareSnapshot Latest { get; }

    /// <summary>Every controllable fan found in the current hardware tree. Replaced (not mutated)
    /// each time the service re-initializes, so callers should re-read this rather than cache it
    /// across a re-initialization.</summary>
    IReadOnlyList<IFanController> Controllers { get; }

    /// <summary>Resets every sensor's observed min/max (the sensors tab's "Reset min/max"
    /// button). Applied on the next tick, from the dedicated background thread.</summary>
    void ResetMinMax();

    /// <summary>
    /// Runs <paramref name="action"/> on the dedicated hardware thread and blocks the caller until
    /// it completes (or <paramref name="timeout"/> elapses). Every <see cref="IFanController"/> call
    /// that does not originate from a <see cref="SnapshotUpdated"/> handler - restoring fans on
    /// exit/suspend/session-end, re-arming after a failure - must go through this instead of calling
    /// the controller directly, so it never races the hardware thread's own tick.
    /// </summary>
    /// <remarks>
    /// If the calling thread already <em>is</em> the hardware thread (e.g. called from within a
    /// <see cref="SnapshotUpdated"/> handler), <paramref name="action"/> runs inline with no
    /// queuing. If the hardware thread does not pick up the command within <paramref name="timeout"/>
    /// (stuck, or not running) and <paramref name="allowDirectFallback"/> is true,
    /// <paramref name="action"/> is run directly on the calling thread as a last-resort fallback - a
    /// safety action (like restoring every fan to default) must still happen somewhere rather than
    /// silently never running. Pass <paramref name="allowDirectFallback"/> as false for a
    /// non-safety command (e.g. arming/re-arming) where running twice - once here, once later when
    /// the queued copy is eventually picked up - would itself be a correctness problem; on timeout
    /// such a command is logged and dropped instead, relying on the queued copy to run eventually.
    /// </remarks>
    void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true);
}
