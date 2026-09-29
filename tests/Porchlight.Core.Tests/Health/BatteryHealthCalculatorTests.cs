using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Health;

public class BatteryHealthCalculatorTests
{
    [Theory]
    [InlineData(50000L, 30000L, 60)]
    [InlineData(50000L, 50000L, 100)]
    [InlineData(50000L, 52000L, 100)]
    [InlineData(50000L, 0L, 0)]
    public void Health_percent_is_clamped_ratio(long design, long full, int expected)
    {
        Assert.Equal(expected, BatteryHealthCalculator.HealthPercent(design, full));
    }

    [Theory]
    [InlineData(null, 100L)]
    [InlineData(100L, null)]
    [InlineData(0L, 100L)]
    [InlineData(-5L, 100L)]
    public void Health_percent_null_when_input_missing(long? design, long? full)
    {
        Assert.Null(BatteryHealthCalculator.HealthPercent(design, full));
    }

    [Fact]
    public void Good_at_80_and_above()
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(100, 80, null, null, BatteryChargeState.Unknown));
        Assert.Equal(BatteryVerdict.Good, a.Verdict);
        Assert.Equal("Good", a.Text);
    }

    [Fact]
    public void Worn_text_mentions_percentage()
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(100, 60, null, null, BatteryChargeState.Unknown));
        Assert.Equal(BatteryVerdict.Worn, a.Verdict);
        Assert.Equal("Worn - holds about 60% of its original charge", a.Text);
    }

    [Fact]
    public void Very_worn_below_50()
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(100, 49, null, null, BatteryChargeState.Unknown));
        Assert.Equal(BatteryVerdict.VeryWorn, a.Verdict);
        Assert.Contains("49%", a.Text);
    }

    [Fact]
    public void Boundary_50_is_worn()
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(100, 50, null, null, BatteryChargeState.Unknown));
        Assert.Equal(BatteryVerdict.Worn, a.Verdict);
    }

    [Fact]
    public void Unknown_without_capacities()
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(null, null, 10, 50, BatteryChargeState.Charging));
        Assert.Equal(BatteryVerdict.Unknown, a.Verdict);
        Assert.Null(a.HealthPercent);
    }

    [Theory]
    [InlineData(1, BatteryChargeState.Discharging)]
    [InlineData(2, BatteryChargeState.PluggedInNotCharging)]
    [InlineData(3, BatteryChargeState.FullyCharged)]
    [InlineData(6, BatteryChargeState.Charging)]
    [InlineData(99, BatteryChargeState.Unknown)]
    [InlineData(null, BatteryChargeState.Unknown)]
    public void Win32_status_mapping(int? status, BatteryChargeState expected)
    {
        Assert.Equal(expected, BatteryHealthCalculator.StateFromWin32Status(status));
    }
}
