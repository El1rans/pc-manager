namespace Porchlight.Core.Health;

/// <summary>What the battery is doing right now (from <c>Win32_Battery.BatteryStatus</c>).</summary>
public enum BatteryChargeState
{
    Unknown,
    Discharging,
    PluggedInNotCharging,
    Charging,
    FullyCharged,
    Low,
    Critical,
}
