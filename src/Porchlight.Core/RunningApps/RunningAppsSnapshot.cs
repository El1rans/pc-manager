namespace Porchlight.Core.RunningApps;

/// <summary>Everything the Running apps page shows for one refresh.</summary>
/// <param name="Apps">The groups, in no particular order.</param>
/// <param name="TotalCpuPercent">CPU in use across the PC, 0..100.</param>
/// <param name="Memory">Physical memory, or null when unknown.</param>
public sealed record RunningAppsSnapshot(IReadOnlyList<RunningApp> Apps, double TotalCpuPercent, MemoryStatus? Memory);
