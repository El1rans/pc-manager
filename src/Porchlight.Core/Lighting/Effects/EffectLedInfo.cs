namespace Porchlight.Core.Lighting.Effects;

/// <summary>One physical LED on a device, as reported by OpenRGB - just enough to name a
/// <see cref="LedPoint"/> ("Key: A", "LED 3").</summary>
/// <param name="Index">The LED's index within its device (matches
/// <see cref="EffectDeviceInfo.Leds"/> and the color array <see cref="IEffectDeviceClient.UpdateLeds"/>
/// expects).</param>
/// <param name="Name">Display name, as reported by OpenRGB (e.g. "Key: A"). Never null or empty -
/// <see cref="OpenRgbEffectDeviceClient"/> falls back to a generic name when OpenRGB reports
/// none.</param>
public sealed record EffectLedInfo(int Index, string Name);
