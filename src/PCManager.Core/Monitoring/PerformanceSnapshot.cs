namespace PCManager.Core.Monitoring;

/// <summary>One tick's worth of live performance numbers. Any field is null when its source is
/// unavailable on this PC (e.g. no GPU Engine counter category) - the dashboard shows "n/a" rather
/// than a misleading zero.</summary>
public sealed record PerformanceSnapshot(
    double? CpuPercent,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    double? GpuPercent,
    double? DiskActivePercent,
    double? DiskReadBytesPerSecond,
    double? DiskWriteBytesPerSecond,
    double? NetworkDownloadBytesPerSecond,
    double? NetworkUploadBytesPerSecond,
    long NetworkTotalDownloadedBytes,
    long NetworkTotalUploadedBytes);
