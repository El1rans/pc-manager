using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Tray;

/// <inheritdoc cref="IQuickStatsProvider"/>
/// <remarks>CPU use comes from <c>GetSystemTimes</c> deltas between successive <see cref="Read"/>
/// calls (the first call has no baseline and reports null), memory from
/// <c>GlobalMemoryStatusEx</c> - both cheap and independent of any performance counter.</remarks>
public sealed partial class QuickStatsProvider(ILogger<QuickStatsProvider> logger) : IQuickStatsProvider
{
    private readonly Lock _gate = new();
    private (ulong Idle, ulong Total)? _previousCpuTimes;

    public QuickStats Read()
    {
        return new QuickStats(ReadCpuPercent(), ReadMemoryPercent(), ReadSystemDrive(out var name), name);
    }

    private double? ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            LogNativeReadFailed("GetSystemTimes");
            return null;
        }

        // Kernel time already includes idle time.
        var current = (Idle: idle, Total: kernel + user);
        lock (_gate)
        {
            var previous = _previousCpuTimes;
            _previousCpuTimes = current;
            if (previous is null || current.Total <= previous.Value.Total)
            {
                return null;
            }

            var totalDelta = (double)(current.Total - previous.Value.Total);
            var idleDelta = (double)(current.Idle - previous.Value.Idle);
            return Math.Clamp((1 - (idleDelta / totalDelta)) * 100, 0, 100);
        }
    }

    private double? ReadMemoryPercent()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            LogNativeReadFailed("GlobalMemoryStatusEx");
            return null;
        }

        return status.MemoryLoad;
    }

    private long? ReadSystemDrive(out string? name)
    {
        name = null;
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            name = root;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            LogDriveReadFailed(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Native call {Call} failed while reading tray quick stats.")]
    private partial void LogNativeReadFailed(string call);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read free space on the system drive for the tray tooltip.")]
    private partial void LogDriveReadFailed(Exception ex);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    // FILETIME is a 64-bit count; ulong out parameters marshal to it directly.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
#pragma warning restore SYSLIB1054
}
