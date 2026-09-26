namespace PCManager.Core.Monitoring;

/// <summary>A single fixed or ready removable drive, as reported by <see cref="IDriveMonitor"/>.</summary>
/// <param name="Name">Root path, e.g. <c>C:\</c>.</param>
/// <param name="Label">Volume label, if any.</param>
/// <param name="Format">File system, e.g. <c>NTFS</c>.</param>
/// <param name="TotalBytes">Total capacity.</param>
/// <param name="FreeBytes">Free space.</param>
/// <param name="IsLow">See <see cref="DriveThresholds.IsLow"/>.</param>
public sealed record DriveSnapshot(
    string Name,
    string? Label,
    string? Format,
    long TotalBytes,
    long FreeBytes,
    bool IsLow);
