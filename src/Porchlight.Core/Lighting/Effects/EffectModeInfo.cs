namespace Porchlight.Core.Lighting.Effects;

/// <summary>One lighting mode a device supports, narrowed to just what <see cref="EffectEngine"/>
/// needs to pick and switch to a per-LED "Direct" mode before rendering.</summary>
/// <param name="Index">The device-relative mode index, passed back to
/// <see cref="IEffectDeviceClient.SetMode"/>.</param>
/// <param name="Name">Display name, as reported by OpenRGB (e.g. "Direct", "Static").</param>
/// <param name="IsPerLed">Whether this mode accepts a per-LED color array via
/// <see cref="IEffectDeviceClient.UpdateLeds"/> (OpenRGB's <c>ColorMode.PerLed</c>) rather than a
/// single mode-specific color.</param>
public sealed record EffectModeInfo(int Index, string Name, bool IsPerLed);
