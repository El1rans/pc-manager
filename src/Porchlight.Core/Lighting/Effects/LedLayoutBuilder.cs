namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Builds a <see cref="LedLayout"/> from one of a device's zones. Pure and unit tested directly -
/// see docs/specs/11-led-effects.md.
/// </summary>
public static class LedLayoutBuilder
{
    /// <summary>
    /// Builds the layout for <paramref name="zone"/>. Never throws:
    /// <list type="bullet">
    /// <item><description>A zero-LED zone (e.g. an unpopulated ARGB header) returns
    /// <see cref="LedLayout.Empty"/>.</description></item>
    /// <item><description>A <see cref="EffectZoneType.Matrix"/> zone places each LED at its
    /// matrix column/row, normalized by the matrix's width/height; a hole (OpenRGB's
    /// <c>0xFFFFFFFF</c>, translated to <c>-1</c> in <see cref="EffectZoneInfo.MatrixLedIndices"/>)
    /// is skipped.</description></item>
    /// <item><description>A <see cref="EffectZoneType.Linear"/> zone places its LEDs in a single
    /// row, evenly spaced left to right (<c>x = index / (count - 1)</c>, or <c>x = 0</c> for a
    /// single-LED zone).</description></item>
    /// <item><description>A <see cref="EffectZoneType.SingleLed"/> zone places its one LED at the
    /// center (0.5, 0.5).</description></item>
    /// </list>
    /// </summary>
    /// <param name="zone">The zone to build a layout for.</param>
    /// <param name="deviceLeds">The owning device's full LED list (<see cref="EffectDeviceInfo.Leds"/>),
    /// used to name each point. May be shorter than expected (defensive - a mismatched device
    /// still gets a generic name rather than throwing).</param>
    public static LedLayout Build(EffectZoneInfo zone, IReadOnlyList<EffectLedInfo> deviceLeds)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(deviceLeds);

        if (zone.LedCount <= 0)
        {
            return LedLayout.Empty;
        }

        return zone.Type switch
        {
            EffectZoneType.Matrix => BuildMatrix(zone, deviceLeds),
            EffectZoneType.SingleLed => BuildSingle(zone, deviceLeds),
            _ => BuildLinear(zone, deviceLeds),
        };
    }

    private static LedLayout BuildMatrix(EffectZoneInfo zone, IReadOnlyList<EffectLedInfo> deviceLeds)
    {
        if (zone.MatrixLedIndices is not { Count: > 0 } indices ||
            zone.MatrixWidth is not { } width || width <= 0 ||
            zone.MatrixHeight is not { } height || height <= 0)
        {
            // Zone claims to be a matrix but has no usable matrix map - fall back to a linear
            // layout of its LEDs rather than producing nothing.
            return BuildLinear(zone, deviceLeds);
        }

        var points = new List<LedPoint>(zone.LedCount);
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var localIndex = indices[(row * width) + col];
                if (localIndex < 0)
                {
                    // Hole in the matrix (OpenRGB's 0xFFFFFFFF) - no LED at this grid cell.
                    continue;
                }

                var deviceLedIndex = zone.LedOffset + localIndex;
                var x = width == 1 ? 0d : (double)col / (width - 1);
                var y = height == 1 ? 0d : (double)row / (height - 1);
                points.Add(new LedPoint(deviceLedIndex, NameFor(deviceLedIndex, deviceLeds), x, y, col, row));
            }
        }

        return new LedLayout(points, width, height);
    }

    private static LedLayout BuildLinear(EffectZoneInfo zone, IReadOnlyList<EffectLedInfo> deviceLeds)
    {
        var count = zone.LedCount;
        var points = new List<LedPoint>(count);
        for (var i = 0; i < count; i++)
        {
            var deviceLedIndex = zone.LedOffset + i;
            var x = count <= 1 ? 0d : (double)i / (count - 1);
            points.Add(new LedPoint(deviceLedIndex, NameFor(deviceLedIndex, deviceLeds), x, 0.5, Col: null, Row: null));
        }

        return new LedLayout(points, ColumnCount: null, RowCount: null);
    }

    private static LedLayout BuildSingle(EffectZoneInfo zone, IReadOnlyList<EffectLedInfo> deviceLeds)
    {
        var deviceLedIndex = zone.LedOffset;
        var point = new LedPoint(deviceLedIndex, NameFor(deviceLedIndex, deviceLeds), 0.5, 0.5, Col: null, Row: null);
        return new LedLayout([point], ColumnCount: null, RowCount: null);
    }

    private static string NameFor(int deviceLedIndex, IReadOnlyList<EffectLedInfo> deviceLeds)
    {
        foreach (var led in deviceLeds)
        {
            if (led.Index == deviceLedIndex)
            {
                return led.Name;
            }
        }

        return $"LED {deviceLedIndex}";
    }
}
