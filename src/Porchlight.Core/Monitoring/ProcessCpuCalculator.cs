namespace Porchlight.Core.Monitoring;

/// <summary>
/// Pure math for turning two <c>TotalProcessorTime</c> readings into a CPU percentage, shared by
/// <see cref="IProcessMonitor"/>. Separated from the process-reading code so the arithmetic is
/// unit-testable without touching real processes.
/// </summary>
public static class ProcessCpuCalculator
{
    /// <summary>
    /// CPU percent used by a process between two samples, normalized so 100% means "all logical
    /// processors busy" (matching Task Manager's per-process percentages, which are not scaled per
    /// core).
    /// </summary>
    public static double CalculateCpuPercent(
        TimeSpan previousTotalProcessorTime,
        TimeSpan currentTotalProcessorTime,
        TimeSpan elapsed,
        int logicalProcessorCount)
    {
        if (elapsed <= TimeSpan.Zero || logicalProcessorCount <= 0)
        {
            return 0;
        }

        var processorTimeDeltaMs = (currentTotalProcessorTime - previousTotalProcessorTime).TotalMilliseconds;
        if (processorTimeDeltaMs < 0)
        {
            // A process ID was reused between samples (old process exited, a new one reused the
            // PID); treat as no data yet rather than reporting a nonsensical negative value.
            return 0;
        }

        var percent = processorTimeDeltaMs / elapsed.TotalMilliseconds / logicalProcessorCount * 100;
        return Math.Clamp(percent, 0, 100);
    }
}
