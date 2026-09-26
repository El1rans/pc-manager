namespace PCManager.Core.Monitoring;

/// <summary>Processes sharing a name (e.g. several tabs of the same browser), rolled into one row.
/// </summary>
/// <param name="Name">Process name, e.g. <c>chrome</c>.</param>
/// <param name="InstanceCount">Number of live processes with this name.</param>
/// <param name="CpuPercent">Combined CPU percent (0-100 per logical processor total, i.e. can exceed
/// 100 only if... it cannot: each process is already clamped to 0-100, so the group's sum can
/// exceed 100 when several instances are each busy).</param>
/// <param name="WorkingSetBytes">Combined working set.</param>
public sealed record ProcessGroupSnapshot(
    string Name,
    int InstanceCount,
    double CpuPercent,
    long WorkingSetBytes);
