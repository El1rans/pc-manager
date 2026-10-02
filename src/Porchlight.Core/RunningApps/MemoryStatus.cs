namespace Porchlight.Core.RunningApps;

/// <summary>Physical memory of the PC, in bytes.</summary>
public sealed record MemoryStatus(long TotalBytes, long AvailableBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);
}
