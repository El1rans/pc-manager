namespace Porchlight.Core.Processes;

/// <summary>
/// Ties <see cref="Components.IRegistryReader"/> (find the package's install directory) and
/// <see cref="IAppLockDetector"/> (find what's locking files in it) together to enrich winget's
/// "app in use" explanation with the actual programs holding the lock, when the app being updated
/// is not itself running - see <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.
/// </summary>
public interface IAppInUseDiagnosticsService
{
    /// <summary>
    /// Returns an enriched explanation (see <see cref="AppInUseExplanation.Build"/>) naming the
    /// programs currently locking <paramref name="displayName"/>'s files, or null if the install
    /// directory couldn't be found, nothing is locked, or the lookup failed for any reason - the
    /// caller should keep the generic "close the app" explanation in that case.
    /// </summary>
    /// <remarks>
    /// The real implementation does its (blocking registry/file/native) work off the calling
    /// thread itself - callers should simply <c>await</c> this rather than wrapping the call in
    /// their own <c>Task.Run</c>. Wrapping it a second time in a caller with no
    /// <see cref="SynchronizationContext"/> of its own (e.g. a unit test with no WPF Dispatcher)
    /// would resume on an arbitrary thread-pool thread instead of back on the caller's thread.
    /// </remarks>
    Task<string?> TryDescribeLockingProcessesAsync(string packageId, string displayName);
}
