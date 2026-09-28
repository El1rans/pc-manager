namespace Porchlight.Core.Lighting;

/// <summary>One lighting mode a device supports (e.g. "Direct", "Static", "Rainbow"), as shown in
/// the device row's mode combo box.</summary>
/// <param name="Index">The device-relative mode index, passed back to
/// <see cref="IOpenRgbClient.SetMode"/>.</param>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
/// <param name="ColorMode">How this mode uses colors - see <see cref="RgbColorMode"/>.</param>
/// <param name="ColorCount">How many colors this mode takes when <see cref="ColorMode"/> is
/// <see cref="RgbColorMode.ModeSpecific"/> (typically 1) or <see cref="RgbColorMode.PerLed"/>
/// (one per LED, handled instead via <see cref="IOpenRgbClient.UpdateLeds"/>). Zero when the mode
/// takes no colors at all.</param>
public sealed record RgbMode(int Index, string Name, RgbColorMode ColorMode = RgbColorMode.None, int ColorCount = 0);
