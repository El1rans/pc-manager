namespace PCManager.Core.Lighting;

/// <summary>One lighting mode a device supports (e.g. "Direct", "Static", "Rainbow"), as shown in
/// the device row's mode combo box.</summary>
/// <param name="Index">The device-relative mode index, passed back to
/// <see cref="IOpenRgbClient.SetMode"/>.</param>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
public sealed record RgbMode(int Index, string Name);
