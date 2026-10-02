using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Porchlight.Core.RunningApps;

/// <inheritdoc cref="IProcessSnapshotSource"/>
public sealed partial class ProcessSnapshotSource(ILogger<ProcessSnapshotSource> logger) : IProcessSnapshotSource
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaxPathChars = 32_768;
    private const int GwOwner = 4;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int DwmwaCloaked = 14;

    /// <summary>Pids already logged as unreadable, so a protected process is reported once, not every tick.</summary>
    private readonly HashSet<int> _loggedPids = [];

    public IReadOnlyList<ProcessSample> Capture()
    {
        var windowOwners = FindWindowOwners();
        var processes = Process.GetProcesses();
        var samples = new List<ProcessSample>(processes.Length);
        var seen = new HashSet<int>();

        foreach (var process in processes)
        {
            using (process)
            {
                var pid = process.Id;
                if (pid == 0)
                {
                    // System Idle Process: not a real workload.
                    continue;
                }

                seen.Add(pid);
                try
                {
                    samples.Add(new ProcessSample(
                        pid,
                        process.ProcessName,
                        ReadPath(pid),
                        ReadOrDefault(pid, "session", () => process.SessionId, -1),
                        windowOwners.Contains(pid),
                        ReadOrDefault<DateTime?>(pid, "start time", () => process.StartTime, null),
                        ReadOrDefault(pid, "cpu time", () => process.TotalProcessorTime, TimeSpan.Zero),
                        ReadOrDefault(pid, "memory", () => process.WorkingSet64, 0L)));
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // Exited while being read; leave it out of this tick.
                    LogUnreadable(ex, pid, "name");
                }
            }
        }

        _loggedPids.RemoveWhere(pid => !seen.Contains(pid));
        return samples;
    }

    private T ReadOrDefault<T>(int pid, string what, Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            if (_loggedPids.Add(pid))
            {
                LogUnreadable(ex, pid, what);
            }

            return fallback;
        }
    }

    private string? ReadPath(int pid)
    {
        using var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle.IsInvalid)
        {
            if (_loggedPids.Add(pid))
            {
                LogUnreadable(null, pid, "path");
            }

            return null;
        }

        var buffer = new char[MaxPathChars];
        var size = buffer.Length;
        return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
    }

    private static HashSet<int> FindWindowOwners()
    {
        var owners = new HashSet<int>();
        EnumWindows(
            (hwnd, lParam) =>
            {
                if (IsWindowVisible(hwnd)
                    && GetWindow(hwnd, GwOwner) == IntPtr.Zero
                    && (GetWindowLong(hwnd, GwlExStyle) & WsExToolWindow) == 0
                    && GetWindowTextLength(hwnd) > 0
                    && !IsCloaked(hwnd))
                {
                    var thread = GetWindowThreadProcessId(hwnd, out var pid);
                    if (thread != 0)
                    {
                        owners.Add((int)pid);
                    }
                }

                return true;
            },
            IntPtr.Zero);
        return owners;
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot read {What} of process {Pid}; using a fallback.")]
    private partial void LogUnreadable(Exception? ex, int pid, string what);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    // Plain DllImport: the LibraryImport generator needs AllowUnsafeBlocks, which this project does not enable.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
#pragma warning restore SYSLIB1054
}
