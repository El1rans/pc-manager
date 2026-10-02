namespace Porchlight.Core.Startup;

/// <summary>What Windows recorded for one process during a boot's startup trace.</summary>
/// <param name="ImagePath">Executable path as recorded (may be a <c>\Device\</c> or <c>\\?\</c> path).</param>
/// <param name="CpuTimeMs">CPU time in milliseconds.</param>
/// <param name="DiskBytes">Disk I/O in bytes.</param>
public sealed record StartupInfoRecord(string ImagePath, double CpuTimeMs, long DiskBytes);
