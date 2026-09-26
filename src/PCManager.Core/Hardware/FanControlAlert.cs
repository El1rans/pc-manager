namespace PCManager.Core.Hardware;

/// <summary>Severity for a fan-safety banner (spec 04: "critical banner when failsafe is active
/// ..., caution banner when running without admin/driver").</summary>
public enum FanControlAlertLevel
{
    Caution,
    Critical,
}

/// <param name="Level">Caution or critical.</param>
/// <param name="Message">Plain-language, ready to show as-is: names which rule/sensor triggered it.</param>
public sealed record FanControlAlert(FanControlAlertLevel Level, string Message);
