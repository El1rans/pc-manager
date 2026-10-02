namespace Porchlight.Core.RunningApps;

/// <summary>Pure math: per-process CPU percentage between two samples.</summary>
public static class CpuUsageCalculator
{
    /// <summary>
    /// CPU percent per pid, where 100 means every logical processor was fully busy (the way Task
    /// Manager shows it). A pid that is new, or whose start time changed (pid reuse), gets 0 for
    /// this tick. Values are clamped to 0..100.
    /// </summary>
    public static IReadOnlyDictionary<int, double> Calculate(
        IReadOnlyDictionary<int, ProcessSample> previous,
        IReadOnlyList<ProcessSample> current,
        TimeSpan elapsed,
        int processorCount)
    {
        var result = new Dictionary<int, double>(current.Count);
        foreach (var sample in current)
        {
            result[sample.Pid] = 0;
            if (elapsed <= TimeSpan.Zero || processorCount <= 0 || !previous.TryGetValue(sample.Pid, out var before))
            {
                continue;
            }

            if (before.StartTime != sample.StartTime)
            {
                continue;
            }

            var deltaMs = (sample.TotalProcessorTime - before.TotalProcessorTime).TotalMilliseconds;
            if (deltaMs <= 0)
            {
                continue;
            }

            result[sample.Pid] = Math.Clamp(deltaMs / elapsed.TotalMilliseconds / processorCount * 100, 0, 100);
        }

        return result;
    }
}
