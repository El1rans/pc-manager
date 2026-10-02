using System.Runtime.InteropServices;

namespace Porchlight.Core.RunningApps;

/// <inheritdoc cref="ISystemMemoryInfo"/>
public sealed class SystemMemoryInfo : ISystemMemoryInfo
{
    public MemoryStatus? Read()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? new MemoryStatus((long)status.TotalPhysicalBytes, (long)status.AvailablePhysicalBytes)
            : null;
    }

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

    // DllImport rather than LibraryImport: the generator would need AllowUnsafeBlocks for a ref struct.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
#pragma warning restore SYSLIB1054
}
