using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.WebConsole;

/// <summary>Everything the web console shows, as served (JSON) by <c>GET /api/stats</c>. Only
/// ever read-only facts about the PC - nothing here can be used to change anything on it.</summary>
/// <param name="TimestampUtc">When this was sampled.</param>
/// <param name="SystemInfo">Machine identity/hardware facts, or null if they could not be read yet.</param>
/// <param name="Performance">Live CPU/memory/GPU/disk/network numbers, or null if sampling failed.</param>
/// <param name="Drives">Fixed and ready removable drives.</param>
/// <param name="TopProcesses">The busiest process groups, as on the dashboard.</param>
/// <param name="IsRestartPending">Whether Windows is waiting for a restart.</param>
/// <param name="Hardware">The Hardware page's "At a glance" tiles (temperatures, power, fans);
/// empty when sensors are unavailable.</param>
public sealed record WebConsoleStats(
    DateTimeOffset TimestampUtc,
    SystemInfo? SystemInfo,
    PerformanceSnapshot? Performance,
    IReadOnlyList<DriveSnapshot> Drives,
    IReadOnlyList<ProcessGroupSnapshot> TopProcesses,
    bool IsRestartPending,
    IReadOnlyList<HardwareSummaryTile> Hardware);
