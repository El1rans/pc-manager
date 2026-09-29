namespace Porchlight.Core.Tray;

/// <summary>The handful of numbers shown in the tray icon's tooltip. Any field is null when it
/// could not be read.</summary>
/// <param name="CpuPercent">Overall CPU use since the previous reading, 0-100.</param>
/// <param name="MemoryPercent">Physical memory in use, 0-100.</param>
/// <param name="SystemDriveFreeBytes">Free space on the Windows drive (usually C:).</param>
/// <param name="SystemDriveName">Its root, e.g. <c>C:\</c>.</param>
public sealed record QuickStats(
    double? CpuPercent,
    double? MemoryPercent,
    long? SystemDriveFreeBytes,
    string? SystemDriveName);
