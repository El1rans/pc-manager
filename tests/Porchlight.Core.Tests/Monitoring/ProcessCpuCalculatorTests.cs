using Xunit;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tests.Monitoring;

public class ProcessCpuCalculatorTests
{
    [Theory]
    [InlineData(1, 1, 1, 100)] // full core saturation on a single core
    [InlineData(1, 1, 4, 25)] // divides by the logical processor count
    [InlineData(4, 1, 2, 100)] // clamps to 100 when multiple cores are saturated
    public void CalculateCpuPercent_scales_by_core_count_and_clamps_to_100(
        int cpuSeconds, int elapsedSeconds, int logicalProcessorCount, double expected)
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.Zero,
            currentTotalProcessorTime: TimeSpan.FromSeconds(cpuSeconds),
            elapsed: TimeSpan.FromSeconds(elapsedSeconds),
            logicalProcessorCount: logicalProcessorCount);

        Assert.Equal(expected, percent);
    }

    [Theory]
    [InlineData(0, 1, 0, 4)] // elapsed is zero
    [InlineData(10, 1, 1, 4)] // the delta is negative (pid reuse)
    public void CalculateCpuPercent_is_zero_for_unusable_samples(
        int previousSeconds, int currentSeconds, int elapsedSeconds, int logicalProcessorCount)
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.FromSeconds(previousSeconds),
            currentTotalProcessorTime: TimeSpan.FromSeconds(currentSeconds),
            elapsed: TimeSpan.FromSeconds(elapsedSeconds),
            logicalProcessorCount: logicalProcessorCount);

        Assert.Equal(0, percent);
    }
}
