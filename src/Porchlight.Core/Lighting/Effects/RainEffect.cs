namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// A falling drop per matrix column, each leaving a fading trail behind it, restarting from the top
/// after it reaches the bottom (with a gap so drops in the same column don't run back-to-back) -
/// see docs/specs/11-led-effects.md. Matrix-only: on a non-matrix layout this renders nothing.
/// </summary>
public sealed class RainEffect : IEffect
{
    private static readonly RgbColor DropColor = new(0x40, 0xA0, 0xFF);

    /// <summary>An irrational fraction (the golden ratio's fractional part) used to spread each
    /// column's drop out of phase from its neighbors, deterministically - no real randomness, so
    /// the effect stays reproducible for tests.</summary>
    private const double PhaseSpread = 0.6180339887498949;

    /// <param name="rowsPerSecond">How fast a drop's head falls, in rows per second. Must be
    /// positive.</param>
    /// <param name="trailLength">How many rows behind the head still glow, fading out. Must be at
    /// least 1.</param>
    public RainEffect(double rowsPerSecond = 8.0, int trailLength = 4)
    {
        if (rowsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowsPerSecond), rowsPerSecond, "Must be positive.");
        }

        if (trailLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(trailLength), trailLength, "Must be at least 1.");
        }

        RowsPerSecond = rowsPerSecond;
        TrailLength = trailLength;
    }

    public string Name => "Rain";

    public double RowsPerSecond { get; }

    public int TrailLength { get; }

    /// <summary>The falling head's row position for the given column/time, including the "gap"
    /// above the grid a drop spends before entering - negative or beyond <paramref name="rowCount"/>
    /// means no drop is currently in the visible grid at that row. Public/static and pure for
    /// tests.</summary>
    public double HeadRowAt(int column, int rowCount, TimeSpan elapsed)
    {
        var cycleLength = rowCount + TrailLength;
        var phase = Fractional(column * PhaseSpread) * cycleLength;
        var position = ((elapsed.TotalSeconds * RowsPerSecond) + phase) % cycleLength;
        return position - TrailLength;
    }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var layout = frame.Layout;
        if (!layout.IsMatrix)
        {
            return;
        }

        var rowCount = layout.RowCount!.Value;
        buffer.Fill(RgbColor.Black);

        for (var i = 0; i < layout.Points.Count; i++)
        {
            var point = layout.Points[i];
            var col = point.Col!.Value;
            var row = point.Row!.Value;

            var headRow = HeadRowAt(col, rowCount, frame.Elapsed);
            var distance = headRow - row;
            if (distance < 0 || distance > TrailLength)
            {
                continue;
            }

            var brightness = 1 - (distance / TrailLength);
            buffer[i] = DropColor.Scale(brightness);
        }
    }

    private static double Fractional(double value)
    {
        var fractional = value - Math.Floor(value);
        return fractional < 0 ? fractional + 1 : fractional;
    }
}
