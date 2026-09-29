namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>
/// One frame of a <see cref="CustomAnimation"/>: a small grid of colors that is stretched over
/// whatever LED layout the device has - see <see cref="Sample"/>. A <c>"fill"</c> frame is a 1x1
/// grid, a <c>"gradient"</c> frame a one-row grid of color stops blended smoothly left to right,
/// and a <c>"rows"</c> frame a pixel-art grid drawn with palette characters.
/// </summary>
public sealed class CustomAnimationFrame
{
    private readonly RgbColor[] _pixels;

    internal CustomAnimationFrame(int width, int height, RgbColor[] pixels, bool isGradient, TimeSpan duration)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
        IsGradient = isGradient;
        Duration = duration;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Whether horizontal neighbors are blended (a <c>"gradient"</c> frame) rather than
    /// sampled as hard-edged pixels.</summary>
    public bool IsGradient { get; }

    public TimeSpan Duration { get; }

    /// <summary>The grid's color at column <paramref name="col"/>, row <paramref name="row"/>.</summary>
    public RgbColor this[int col, int row] => _pixels[(row * Width) + col];

    /// <summary>
    /// The color at normalized position (<paramref name="x"/>, <paramref name="y"/>), both 0..1 -
    /// the same space <see cref="LedPoint.X"/>/<see cref="LedPoint.Y"/> use. The grid's first and
    /// last columns/rows line up exactly with the layout's edges, so a grid the same size as a
    /// keyboard's matrix (e.g. 27x7 for a Logitech G915) maps one character to one key, a single-row
    /// grid maps across a linear strip, and any other size is stretched to fit.
    /// </summary>
    public RgbColor Sample(double x, double y)
    {
        x = double.IsFinite(x) ? Math.Clamp(x, 0, 1) : 0;
        y = double.IsFinite(y) ? Math.Clamp(y, 0, 1) : 0;
        var row = (int)Math.Round(y * (Height - 1), MidpointRounding.AwayFromZero);

        if (IsGradient && Width > 1)
        {
            var position = x * (Width - 1);
            var left = (int)Math.Floor(position);
            var right = Math.Min(left + 1, Width - 1);
            return Lerp(this[left, row], this[right, row], position - left);
        }

        var col = (int)Math.Round(x * (Width - 1), MidpointRounding.AwayFromZero);
        return this[col, row];
    }

    /// <summary>Linear blend from <paramref name="from"/> (t = 0) to <paramref name="to"/> (t = 1).</summary>
    public static RgbColor Lerp(RgbColor from, RgbColor to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new RgbColor(Channel(from.R, to.R, t), Channel(from.G, to.G, t), Channel(from.B, to.B, t));
    }

    private static byte Channel(byte from, byte to, double t) =>
        (byte)Math.Clamp(Math.Round(from + ((to - from) * t), MidpointRounding.AwayFromZero), 0, 255);
}
