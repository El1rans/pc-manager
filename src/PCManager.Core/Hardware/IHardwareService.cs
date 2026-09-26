namespace PCManager.Core.Hardware;

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
}
