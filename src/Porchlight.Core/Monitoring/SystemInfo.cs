namespace Porchlight.Core.Monitoring;

/// <summary>Static machine identity/hardware facts shown on the dashboard's "System" card. Any
/// field whose WMI query failed is <c>"Unknown"</c> (or empty/null for collections/dates).</summary>
public sealed record SystemInfo(
    string ComputerName,
    string OsCaption,
    string OsBuild,
    string Manufacturer,
    string Model,
    string CpuName,
    int PhysicalCores,
    int LogicalProcessors,
    IReadOnlyList<string> GpuNames,
    long TotalRamBytes,
    DateTime? LastBootTimeUtc);
