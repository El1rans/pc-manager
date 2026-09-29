using System.Globalization;

namespace Porchlight.Core.Health;

/// <summary>Pure battery-wear calculation - see <c>docs/specs/14-system-health.md</c>.</summary>
public static class BatteryHealthCalculator
{
    /// <summary>At or above this health percent the battery is "Good".</summary>
    public const int GoodMinPercent = 80;

    /// <summary>At or above this (and below <see cref="GoodMinPercent"/>) it is "Worn"; below it "Very worn".</summary>
    public const int WornMinPercent = 50;

    private const int PercentScale = 100;

    /// <summary>Full-charge / design capacity as a whole percent clamped to 0-100, or null when either
    /// value is missing or the design capacity is not positive (a battery may report a full-charge
    /// figure slightly above design - that is shown as 100, not 103).</summary>
    public static int? HealthPercent(long? designCapacity, long? fullChargeCapacity)
    {
        if (designCapacity is not > 0 || fullChargeCapacity is null or < 0)
        {
            return null;
        }

        var percent = (double)fullChargeCapacity.Value * PercentScale / designCapacity.Value;
        return (int)Math.Clamp(Math.Round(percent, MidpointRounding.AwayFromZero), 0, PercentScale);
    }

    public static BatteryAssessment Assess(BatteryReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (HealthPercent(reading.DesignCapacityMilliwattHours, reading.FullChargeCapacityMilliwattHours)
            is not { } percent)
        {
            return new BatteryAssessment(null, BatteryVerdict.Unknown, "Unknown - this battery doesn't report its wear");
        }

        var text = percent switch
        {
            >= GoodMinPercent => "Good",
            >= WornMinPercent => string.Create(
                CultureInfo.InvariantCulture, $"Worn - holds about {percent}% of its original charge"),
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"Very worn - holds only about {percent}% of its original charge. Consider replacing the battery"),
        };

        var verdict = percent switch
        {
            >= GoodMinPercent => BatteryVerdict.Good,
            >= WornMinPercent => BatteryVerdict.Worn,
            _ => BatteryVerdict.VeryWorn,
        };

        return new BatteryAssessment(percent, verdict, text);
    }

    /// <summary>Plain text for the current charge state, e.g. "Charging".</summary>
    public static string DescribeState(BatteryChargeState state) => state switch
    {
        BatteryChargeState.Discharging => "Running on battery",
        BatteryChargeState.PluggedInNotCharging => "Plugged in",
        BatteryChargeState.Charging => "Charging",
        BatteryChargeState.FullyCharged => "Fully charged",
        BatteryChargeState.Low => "Running low",
        BatteryChargeState.Critical => "Almost empty",
        _ => "Unknown",
    };

    /// <summary>Maps <c>Win32_Battery.BatteryStatus</c> (1 discharging, 2 on AC, 3 fully charged,
    /// 4 low, 5 critical, 6-9 charging, 10 undefined, 11 partially charged) to a state.</summary>
    public static BatteryChargeState StateFromWin32Status(int? status) => status switch
    {
        1 => BatteryChargeState.Discharging,
        2 => BatteryChargeState.PluggedInNotCharging,
        3 => BatteryChargeState.FullyCharged,
        4 => BatteryChargeState.Low,
        5 => BatteryChargeState.Critical,
        6 or 7 or 8 or 9 => BatteryChargeState.Charging,
        _ => BatteryChargeState.Unknown,
    };
}
