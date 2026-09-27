namespace Porchlight.Core.Hardware;

/// <summary>
/// How one fan should be driven, resolved from <c>HardwareSettings.FanProfiles</c> into a form
/// <see cref="FanControlEngine"/> can evaluate directly (an already-validated <see cref="FanCurve"/>
/// rather than a raw list of points).
/// </summary>
/// <param name="FanId">Matches an <see cref="IFanController.Id"/>.</param>
/// <param name="Mode">Default (BIOS), Fixed, or Curve.</param>
/// <param name="FixedPercent">Used when <paramref name="Mode"/> is <see cref="FanMode.Fixed"/>.</param>
/// <param name="SourceSensorId">Temperature sensor id used when <paramref name="Mode"/> is
/// <see cref="FanMode.Curve"/>.</param>
/// <param name="Curve">The curve used when <paramref name="Mode"/> is <see cref="FanMode.Curve"/>.</param>
public sealed record FanProfile(
    string FanId,
    FanMode Mode,
    double FixedPercent = 0,
    string? SourceSensorId = null,
    FanCurve? Curve = null);
