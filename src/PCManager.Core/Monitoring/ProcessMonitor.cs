using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Monitoring;

/// <inheritdoc cref="IProcessMonitor"/>
public sealed partial class ProcessMonitor(ILogger<ProcessMonitor> logger) : IProcessMonitor
{
    private readonly Dictionary<int, TimeSpan> _previousCpuTimes = [];
    private readonly HashSet<int> _deniedProcessIds = [];
    private DateTime _previousSampleUtc = DateTime.UtcNow;
    private bool _hasPreviousSample;

    public IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var now = DateTime.UtcNow;
        var elapsed = now - _previousSampleUtc;
        _previousSampleUtc = now;

        var logicalProcessorCount = Environment.ProcessorCount;
        var currentCpuTimes = new Dictionary<int, TimeSpan>();
        var groups = new Dictionary<string, (int Count, double Cpu, long WorkingSet)>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (_deniedProcessIds.Contains(process.Id))
                {
                    continue;
                }

                string name;
                TimeSpan totalProcessorTime;
                long workingSet;
                try
                {
                    name = process.ProcessName;
                    totalProcessorTime = process.TotalProcessorTime;
                    workingSet = process.WorkingSet64;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Access denied (system processes) or the process exited mid-enumeration; cache
                    // the PID so it is not retried every tick.
                    _deniedProcessIds.Add(process.Id);
                    LogProcessUnreadable(ex, process.Id);
                    continue;
                }

                currentCpuTimes[process.Id] = totalProcessorTime;

                var cpuPercent = _hasPreviousSample && _previousCpuTimes.TryGetValue(process.Id, out var previousTime)
                    ? ProcessCpuCalculator.CalculateCpuPercent(previousTime, totalProcessorTime, elapsed, logicalProcessorCount)
                    : 0;

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

        return groups
            .Select(entry => new ProcessGroupSnapshot(entry.Key, entry.Value.Count, entry.Value.Cpu, entry.Value.WorkingSet))
            .OrderByDescending(group => group.CpuPercent)
            .ThenByDescending(group => group.WorkingSetBytes)
            .Take(count)
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot read process {Pid}; excluding from future samples.")]
    private partial void LogProcessUnreadable(Exception ex, int pid);
}
