namespace Porchlight.Core.Lighting;

/// <summary>One lighting zone on a device (e.g. a keyboard's per-key zone). Milestone 05's own UI
/// does not offer per-zone control, only per-device and "all devices"; <see cref="IsMatrix"/> is
/// read by the LED effects UI (docs/specs/11-led-effects.md) to decide whether matrix-only effects
/// (e.g. Pac-Man, Rain) can be offered for a device.</summary>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
/// <param name="LedCount">Number of LEDs in the zone.</param>
/// <param name="IsMatrix">Whether this is a 2D matrix zone (e.g. a keyboard's per-key grid) rather
/// than a linear strip or single LED.</param>
public sealed record RgbZone(string Name, int LedCount, bool IsMatrix = false);
