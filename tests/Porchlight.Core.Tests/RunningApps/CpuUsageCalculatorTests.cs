using Porchlight.Core.RunningApps;
using Xunit;
using static Porchlight.Core.Tests.RunningApps.RunningAppsTestData;

namespace Porchlight.Core.Tests.RunningApps;

public sealed class CpuUsageCalculatorTests
{
    private static Dictionary<int, ProcessSample> Prev(params ProcessSample[] samples) => samples.ToDictionary(s => s.Pid);

    [Fact]
    public void Calculate_NormalisesByProcessorCount()
    {
        // 2 CPU-seconds over 2 s on 4 cores = one core fully busy = 25%.
        var result = CpuUsageCalculator.Calculate(Prev(Sample(1, cpuSeconds: 1)), [Sample(1, cpuSeconds: 3)], TimeSpan.FromSeconds(2), 4);

        Assert.Equal(25, result[1], 3);
    }

    [Fact]
    public void Calculate_NewPid_IsZero()
    {
        var result = CpuUsageCalculator.Calculate(Prev(), [Sample(1, cpuSeconds: 50)], TimeSpan.FromSeconds(2), 4);

        Assert.Equal(0, result[1]);
    }

    [Fact]
    public void Calculate_ReusedPid_IsZero()
    {
        var before = Sample(1, cpuSeconds: 100);
        var after = Sample(1, cpuSeconds: 1, start: Started.AddMinutes(5));

        var result = CpuUsageCalculator.Calculate(Prev(before), [after], TimeSpan.FromSeconds(2), 4);

        Assert.Equal(0, result[1]);
    }

    [Fact]
    public void Calculate_ClampsToHundred()
    {
        var result = CpuUsageCalculator.Calculate(Prev(Sample(1, cpuSeconds: 0)), [Sample(1, cpuSeconds: 100)], TimeSpan.FromSeconds(1), 2);

        Assert.Equal(100, result[1]);
    }

    [Fact]
    public void Calculate_CpuTimeWentBackwardsOrNoElapsedTime_IsZero()
    {
        var backwards = CpuUsageCalculator.Calculate(Prev(Sample(1, cpuSeconds: 5)), [Sample(1, cpuSeconds: 4)], TimeSpan.FromSeconds(2), 4);
        var noTime = CpuUsageCalculator.Calculate(Prev(Sample(1, cpuSeconds: 1)), [Sample(1, cpuSeconds: 2)], TimeSpan.Zero, 4);

        Assert.Equal(0, backwards[1]);
        Assert.Equal(0, noTime[1]);
    }
}
