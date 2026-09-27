namespace PCManager.Core.Hardware;

/// <summary>One point of a <see cref="FanCurve"/>. Points snap to 1 C and 1% in the editor UI; this
/// type itself does not round - <see cref="FanCurve"/> validation only requires strictly
/// increasing temperatures and non-decreasing percentages.</summary>
public sealed record FanCurvePoint(double TemperatureC, double Percent);
