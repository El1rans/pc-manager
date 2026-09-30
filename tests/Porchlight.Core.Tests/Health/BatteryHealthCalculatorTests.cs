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

    [Theory]
    [InlineData(80, BatteryVerdict.Good, "Good")] // Good at 80 and above.
    [InlineData(60, BatteryVerdict.Worn, "Worn - holds about 60% of its original charge")] // Worn text mentions the percentage.
    [InlineData(50, BatteryVerdict.Worn, "Worn - holds about 50% of its original charge")] // 50 is the worn boundary.
    [InlineData(49, BatteryVerdict.VeryWorn, "Very worn - holds only about 49% of its original charge. Consider replacing the battery")] // Very worn below 50.
    public void Assess_verdict_and_text_follow_health_percent(long fullCharge, BatteryVerdict verdict, string text)
    {
        var a = BatteryHealthCalculator.Assess(new BatteryReading(100, fullCharge, null, null, BatteryChargeState.Unknown));
        Assert.Equal(verdict, a.Verdict);
        Assert.Equal(text, a.Text);
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
