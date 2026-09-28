namespace Porchlight.Core.Processes;

/// <summary>
/// Finds which running programs are holding a lock on files inside an application's install
/// directory, via the Windows Restart Manager API - used to make winget's "app in use" outcome
/// actionable when the app actually locking the files is not the app being updated itself (e.g. a
/// background virtual-camera DLL held open by another program entirely). Behind an interface so
/// callers can fake it in tests instead of depending on real running processes - see
/// <see cref="IAppInUseDiagnosticsService"/> and <c>docs/specs/09-friendly-update-outcomes.md</c>'s
/// addendum.
/// </summary>
public interface IAppLockDetector
{
    /// <summary>
    /// Returns the friendly names (Restart Manager's <c>RM_PROCESS_INFO.strAppName</c>) of programs
    /// currently holding a lock on files under <paramref name="installLocation"/>, deduplicated, in
    /// no particular guaranteed order. Returns an empty list if nothing is locked, the directory
    /// doesn't exist or has no matching files, or the lookup itself fails for any reason - never
    /// throws (see <see cref="RestartManagerLockDetector"/>). Never terminates or otherwise affects
    /// any of the processes it finds.
    /// </summary>
    IReadOnlyList<string> FindLockingProcessNames(string installLocation);
}
