namespace Porchlight.Core.Lighting;

/// <summary>How a mode uses the colors assigned to it, mirrored from OpenRGB's own <c>ColorMode</c>
/// so <see cref="LightingService"/> knows whether to write per-LED colors (<see cref="PerLed"/>,
/// e.g. "Direct") or one color via the mode itself (<see cref="ModeSpecific"/>, e.g. "Static" on a
/// GPU or RAM stick that has no addressable per-LED direct mode).</summary>
public enum RgbColorMode
{
    None,
    PerLed,
    ModeSpecific,
    Random,
}
