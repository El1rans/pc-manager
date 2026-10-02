namespace Porchlight.Core.RunningApps;

/// <summary>Processes that share one executable, with their summed usage and the safety verdict.</summary>
/// <param name="Key">Stable id: the lower-cased executable path, or "name:&lt;name&gt;" when the path is unknown.</param>
/// <param name="Name">Process name of the first member (fallback display name).</param>
/// <param name="ExecutablePath">Executable path, or null when unknown.</param>
/// <param name="Section">Page section.</param>
/// <param name="CanEnd">False for Windows, critical, services-session and Porchlight's own processes.</param>
/// <param name="CpuPercent">Summed CPU percent (0..100).</param>
/// <param name="MemoryBytes">Summed memory.</param>
/// <param name="Members">The processes in the group.</param>
public sealed record ProcessGroup(
    string Key,
    string Name,
    string? ExecutablePath,
    RunningAppSection Section,
    bool CanEnd,
    double CpuPercent,
    long MemoryBytes,
    IReadOnlyList<ProcessSample> Members);
