using Xunit;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tests.Monitoring;

public class ProcessCpuCalculatorTests
{
    [Fact]
    public void CalculateCpuPercent_full_core_saturation_on_single_core_is_100_percent()
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.Zero,
            currentTotalProcessorTime: TimeSpan.FromSeconds(1),
            elapsed: TimeSpan.FromSeconds(1),
            logicalProcessorCount: 1);

        Assert.Equal(100, percent);
    }

    [Fact]
    public void CalculateCpuPercent_divides_by_logical_processor_count()
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.Zero,
            currentTotalProcessorTime: TimeSpan.FromSeconds(1),
            elapsed: TimeSpan.FromSeconds(1),
            logicalProcessorCount: 4);

        Assert.Equal(25, percent);
    }

    [Fact]
    public void CalculateCpuPercent_clamps_to_100_when_multiple_cores_saturated()
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.Zero,
            currentTotalProcessorTime: TimeSpan.FromSeconds(4),
            elapsed: TimeSpan.FromSeconds(1),
            logicalProcessorCount: 2);

        Assert.Equal(100, percent);
    }

    [Fact]
    public void CalculateCpuPercent_is_zero_when_elapsed_is_zero()
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.Zero, 4);

        Assert.Equal(0, percent);
    }

    [Fact]
    public void CalculateCpuPercent_is_zero_when_the_delta_is_negative_pid_reuse()
    {
        var percent = ProcessCpuCalculator.CalculateCpuPercent(
            previousTotalProcessorTime: TimeSpan.FromSeconds(10),
            currentTotalProcessorTime: TimeSpan.FromSeconds(1),
            elapsed: TimeSpan.FromSeconds(1),
            logicalProcessorCount: 4);

        Assert.Equal(0, percent);
    }
}
