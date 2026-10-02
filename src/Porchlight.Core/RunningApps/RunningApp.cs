namespace Porchlight.Core.RunningApps;

/// <summary>One row of the Running apps page: all processes of one program.</summary>
/// <param name="Key">Stable group key; pass it to <see cref="IRunningAppsService.EndAsync"/>.</param>
/// <param name="Name">Friendly name.</param>
/// <param name="ExecutablePath">Program path, or null when unknown.</param>
/// <param name="Section">Section of the page.</param>
/// <param name="CanEnd">Whether "End task" is allowed.</param>
/// <param name="ProcessCount">Number of processes in the group.</param>
/// <param name="CpuPercent">Summed CPU percent, 0..100.</param>
/// <param name="MemoryBytes">Summed memory.</param>
public sealed record RunningApp(
    string Key,
    string Name,
    string? ExecutablePath,
    RunningAppSection Section,
    bool CanEnd,
    int ProcessCount,
    double CpuPercent,
    long MemoryBytes);
