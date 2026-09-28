namespace Porchlight.Core.Lighting.Effects;

/// <summary>One lighting zone on a device, carrying enough of OpenRGB's own zone data (type, LED
/// offset, matrix map) for <see cref="LedLayoutBuilder"/> to turn it into a <see cref="LedLayout"/>.
/// Deliberately separate from <c>Porchlight.Core.Lighting.RgbZone</c> (the Lighting page's own,
/// simpler zone DTO) - see docs/specs/11-led-effects.md's "why a separate device client" note.</summary>
/// <param name="Index">The zone's index on its device.</param>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
/// <param name="Type">The zone's shape.</param>
/// <param name="LedCount">How many LEDs the zone has. Zero for, e.g., an unpopulated ARGB header -
/// <see cref="LedLayoutBuilder"/> returns an empty layout for these rather than throwing.</param>
/// <param name="LedOffset">The index, into the device's own <see cref="EffectDeviceInfo.Leds"/> and
/// color array, of this zone's first LED. Zones are laid out contiguously in device LED order, so
/// this is the sum of every earlier zone's <see cref="LedCount"/>.</param>
/// <param name="MatrixWidth">The matrix's column count. Only set when <see cref="Type"/> is
/// <see cref="EffectZoneType.Matrix"/>.</param>
/// <param name="MatrixHeight">The matrix's row count. Only set when <see cref="Type"/> is
/// <see cref="EffectZoneType.Matrix"/>.</param>
/// <param name="MatrixLedIndices">
/// The matrix map flattened row-major (length <c>MatrixWidth * MatrixHeight</c>): entry
/// <c>row * MatrixWidth + col</c> is the zone-relative LED index at that grid cell, or <c>-1</c>
/// for an empty slot (OpenRGB's own <c>0xFFFFFFFF</c> hole marker, translated here). Only set when
/// <see cref="Type"/> is <see cref="EffectZoneType.Matrix"/>.
/// </param>
public sealed record EffectZoneInfo(
    int Index,
    string Name,
    EffectZoneType Type,
    int LedCount,
    int LedOffset,
    int? MatrixWidth,
    int? MatrixHeight,
    IReadOnlyList<int>? MatrixLedIndices);
