using Xunit;
using PCManager.Core.Monitoring;

namespace PCManager.Core.Tests.Monitoring;

public class GpuEngineAggregatorTests
{
    [Fact]
    public void Aggregate_sums_instances_within_the_same_engine_type()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_luid_0x1_phys_0_eng_0_engtype_3D", 10),
            ("pid_200_luid_0x1_phys_0_eng_0_engtype_3D", 15),
        };

        Assert.Equal(25, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_reports_the_busiest_engine_type()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_engtype_3D", 10),
            ("pid_200_engtype_VideoDecode", 60),
            ("pid_300_engtype_VideoDecode", 25),
        };

        // engtype_3D totals 10, engtype_VideoDecode totals 85 - the max wins.
        Assert.Equal(85, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_clamps_to_100()
    {
        var samples = new (string, double)[]
        {
            ("pid_100_engtype_3D", 90),
            ("pid_200_engtype_3D", 90),
        };

        Assert.Equal(100, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_ignores_instances_without_an_engine_type()
    {
        var samples = new (string, double)[] { ("_Total", 999) };

        Assert.Equal(0, GpuEngineAggregator.Aggregate(samples));
    }

    [Fact]
    public void Aggregate_of_empty_input_is_zero() =>
        Assert.Equal(0, GpuEngineAggregator.Aggregate([]));

    [Theory]
    [InlineData("pid_1_luid_0x1_phys_0_eng_0_engtype_3D", "3D")]
    [InlineData("pid_1_engtype_VideoDecode", "VideoDecode")]
    [InlineData("_Total", null)]
    public void ExtractEngineType_parses_the_suffix(string instanceName, string? expected) =>
        Assert.Equal(expected, GpuEngineAggregator.ExtractEngineType(instanceName));
}
