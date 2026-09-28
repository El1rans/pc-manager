using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Monitoring;

/// <inheritdoc cref="IProcessMonitor"/>
public sealed partial class ProcessMonitor(ILogger<ProcessMonitor> logger) : IProcessMonitor
{
    /// <summary>Win32 ERROR_ACCESS_DENIED.</summary>
    private const int AccessDeniedErrorCode = 5;

    private readonly Dictionary<int, TimeSpan> _previousCpuTimes = [];

    /// <summary>PIDs (with the name they had when denied) whose CPU-time read was access-denied, so
    /// it is not retried every tick. Keyed by pid; pruned each sample to only PIDs still running, so
    /// a reused PID is never permanently excluded.</summary>
    private readonly Dictionary<int, string> _deniedCpuTimeProcesses = [];

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _previousElapsed = TimeSpan.Zero;
    private bool _hasPreviousSample;

    public IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var now = _stopwatch.Elapsed;
        var elapsed = now - _previousElapsed;
        _previousElapsed = now;

        var logicalProcessorCount = Environment.ProcessorCount;
        var currentCpuTimes = new Dictionary<int, TimeSpan>();
        var groups = new Dictionary<string, (int Count, double Cpu, long WorkingSet)>(StringComparer.OrdinalIgnoreCase);
        var seenPids = new HashSet<int>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0)
                {
                    // System Idle Process - not a real workload, and its name/CPU reads are odd.
                    continue;
                }

                seenPids.Add(process.Id);

                string name;
                long workingSet;
                try
                {
                    name = process.ProcessName;
                    workingSet = process.WorkingSet64;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // No name to key a denial cache by, and this is usually transient (the process
                    // exiting mid-enumeration); just skip this tick and retry next time rather than
                    // risk permanently hiding a PID that gets reused by a readable process.
                    LogProcessUnreadable(ex, process.Id);
                    continue;
                }

                double cpuPercent;
                if (_deniedCpuTimeProcesses.ContainsKey(process.Id))
                {
                    cpuPercent = 0;
                }
                else
                {
                    try
                    {
                        var totalProcessorTime = process.TotalProcessorTime;
                        currentCpuTimes[process.Id] = totalProcessorTime;
                        cpuPercent = _hasPreviousSample && _previousCpuTimes.TryGetValue(process.Id, out var previousTime)
                            ? ProcessCpuCalculator.CalculateCpuPercent(previousTime, totalProcessorTime, elapsed, logicalProcessorCount)
                            : 0;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedErrorCode)
                    {
                        // Access denied reading CPU time only (common for elevated/system processes
                        // when Porchlight itself is not elevated) - still show the name and memory,
                        // and stop retrying the CPU-time read for this (pid, name).
                        _deniedCpuTimeProcesses[process.Id] = name;
                        cpuPercent = 0;
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
                    {
                        // The process exited between reading its name and its CPU time; not cached,
                        // since the PID may be legitimately reused by a readable process next tick.
                        LogProcessCpuTimeUnreadable(ex, process.Id);
                        cpuPercent = 0;
                    }
                }

                groups.TryGetValue(name, out var existing);
                groups[name] = (existing.Count + 1, existing.Cpu + cpuPercent, existing.WorkingSet + workingSet);
            }
        }

        _previousCpuTimes.Clear();
        foreach (var (pid, time) in currentCpuTimes)
        {
            _previousCpuTimes[pid] = time;
        }

        _hasPreviousSample = true;

        // Prune the denial cache to PIDs still running, so a reused PID is re-tried rather than
        // permanently treated as access-denied.
        foreach (var deniedPid in _deniedCpuTimeProcesses.Keys.Where(pid => !seenPids.Contains(pid)).ToList())
        {
            _deniedCpuTimeProcesses.Remove(deniedPid);
        }

        return groups
            .Select(entry => new ProcessGroupSnapshot(entry.Key, entry.Value.Count, entry.Value.Cpu, entry.Value.WorkingSet))
            .OrderByDescending(group => group.CpuPercent)
            .ThenByDescending(group => group.WorkingSetBytes)
            .Take(count)
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot read process {Pid}; skipping this tick.")]
    private partial void LogProcessUnreadable(Exception ex, int pid);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot read CPU time for process {Pid}; reporting 0% this tick.")]
    private partial void LogProcessCpuTimeUnreadable(Exception ex, int pid);
}
