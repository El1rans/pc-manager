namespace Porchlight.Core.Hardware;

/// <summary>
/// Thin abstraction over "what is currently running on this PC" (process names and Windows
/// service names), so <see cref="IFanControlConflictDetector"/> can be unit tested with a fake
/// instead of calling <c>System.Diagnostics.Process</c>/WMI directly. Read-only: nothing in this
/// feature ever stops, kills, or otherwise modifies a process or service it finds - see the
/// conflict detector's own remarks.
/// </summary>
public interface IRunningSoftwareLister
{
    /// <summary>Names of currently running processes (e.g. <c>Process.ProcessName</c> - no
    /// ".exe" suffix), for a case-insensitive lookup by the caller.</summary>
    IReadOnlyCollection<string> GetRunningProcessNames();

    /// <summary>Names (the short service name, not the display name) of Windows services
    /// currently in the running state, for a case-insensitive lookup by the caller.</summary>
    IReadOnlyCollection<string> GetRunningServiceNames();
}
