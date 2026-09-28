namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// A device zone's LEDs, translated into normalized 2D positions an <see cref="IEffect"/> can
/// render against without knowing anything about OpenRGB - see <see cref="LedLayoutBuilder"/>.
/// </summary>
/// <param name="Points">Every LED in the zone, in the same order an <see cref="IEffect"/>'s
/// render buffer must use (buffer index <c>i</c> corresponds to <c>Points[i]</c>). Empty for a
/// zero-LED zone (e.g. an unpopulated ARGB header) - never null, never throws.</param>
/// <param name="ColumnCount">The matrix's column count. Only set when every point came from a
/// <see cref="EffectZoneType.Matrix"/> zone - matrix-only effects (see docs/specs/11-led-effects.md)
/// check this for null to fall back gracefully on a non-matrix device.</param>
/// <param name="RowCount">The matrix's row count. Only set alongside <see cref="ColumnCount"/>.</param>
public sealed record LedLayout(IReadOnlyList<LedPoint> Points, int? ColumnCount, int? RowCount)
{
    public static readonly LedLayout Empty = new([], null, null);

    /// <summary>How many LEDs the layout covers - the length an <see cref="IEffect"/>'s render
    /// buffer must have.</summary>
    public int Count => Points.Count;

    /// <summary>Whether this layout came from a matrix zone, i.e. every <see cref="LedPoint"/> has
    /// a <see cref="LedPoint.Col"/>/<see cref="LedPoint.Row"/>.</summary>
    public bool IsMatrix => ColumnCount is > 0 && RowCount is > 0;
}
