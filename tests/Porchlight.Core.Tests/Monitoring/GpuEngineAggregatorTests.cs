using Xunit;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tests.Monitoring;

public class GpuEngineAggregatorTests
{
    [Fact]
    public void Aggregate_sums_processes_sharing_the_same_physical_engine()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_luid_0x1_phys_0_eng_0_engtype_3D", 10),
            ("pid_200_luid_0x1_phys_0_eng_0_engtype_3D", 15),
        };

        Assert.Equal(25, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_reports_the_busiest_physical_engine()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_luid_0x1_phys_0_eng_0_engtype_3D", 10),
            ("pid_200_luid_0x1_phys_0_eng_1_engtype_VideoDecode", 60),
            ("pid_300_luid_0x1_phys_0_eng_1_engtype_VideoDecode", 25),
        };

        // eng_0/3D totals 10, eng_1/VideoDecode totals 85 - the max wins.
        Assert.Equal(85, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_keeps_two_adapters_separate_and_reports_the_busiest_engine_overall()
    {
        // A discrete GPU (luid 0x1) mostly idle, an integrated GPU (luid 0x2) at 70% on one engine.
        // Summing across adapters would wrongly inflate or blend unrelated engines; the correct
        // Task Manager-equivalent answer is the single busiest physical engine, 70.
        var samples = new (string, double)[]
        {
            ("pid_100_luid_0x1_phys_0_eng_0_engtype_3D", 5),
            ("pid_200_luid_0x2_phys_0_eng_0_engtype_3D", 70),
        };

        Assert.Equal(70, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_clamps_to_100()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_luid_0x1_phys_0_eng_0_engtype_3D", 90),
            ("pid_200_luid_0x1_phys_0_eng_0_engtype_3D", 90),
        };

        Assert.Equal(100, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_ignores_instances_without_a_pid_prefix()
    {
        var samples = new (string, double)[] { ("_Total", 999) };

        Assert.Equal(0, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_of_empty_input_is_zero() =>
        Assert.Equal(0, GpuEngineAggregator.Aggregate([]));

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000C2A3_phys_0_eng_0_engtype_3D", "luid_0x00000000_0x0000C2A3_phys_0_eng_0_engtype_3D")]
    [InlineData("pid_1_engtype_VideoDecode", "engtype_VideoDecode")]
    [InlineData("_Total", null)]
    [InlineData("pid_", null)]
    public void ExtractPhysicalEngineKey_strips_only_the_pid_prefix(string instanceName, string? expected) =>
        Assert.Equal(expected, GpuEngineAggregator.ExtractPhysicalEngineKey(instanceName));
}
