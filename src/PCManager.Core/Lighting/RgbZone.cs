namespace PCManager.Core.Lighting;

/// <summary>One lighting zone on a device (e.g. a keyboard's per-key zone). Informational only -
/// milestone 05's UI does not offer per-zone control, only per-device and "all devices".</summary>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
/// <param name="LedCount">Number of LEDs in the zone.</param>
public sealed record RgbZone(string Name, int LedCount);
