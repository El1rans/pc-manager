namespace PCManager.Core.Components;

/// <summary>
/// Whether a named process is currently running, behind an interface so
/// <see cref="ComponentState.Running"/> detection is unit-testable without starting real
/// processes.
/// </summary>
public interface IProcessProbe
{
    /// <summary>True if at least one process named <paramref name="processName"/> (no ".exe") is
    /// currently running.</summary>
    bool IsRunning(string processName);
}
