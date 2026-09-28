namespace Porchlight.Core.Lighting.Effects;

/// <summary>A rainbow that scrolls across the layout's normalized X axis over time - see
/// docs/specs/11-led-effects.md.</summary>
public sealed class RainbowWaveEffect : IEffect
{
    /// <summary>How many full hue cycles per second at <c>Speed = 1</c>.</summary>
    private const double BaseCyclesPerSecond = 0.2;

    /// <param name="speed">Scales how fast the rainbow scrolls; 1.0 is the default pace, 2.0 is
    /// twice as fast, etc. Must be positive.</param>
    /// <param name="reverse">Scrolls right-to-left instead of left-to-right.</param>
    public RainbowWaveEffect(double speed = 1.0, bool reverse = false)
    {
        if (speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        Speed = speed;
        Reverse = reverse;
    }

    public string Name => "Rainbow wave";

    public double Speed { get; }

    public bool Reverse { get; }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var layout = frame.Layout;
        var direction = Reverse ? -1d : 1d;
        var timeOffset = frame.Elapsed.TotalSeconds * BaseCyclesPerSecond * Speed * direction;

        for (var i = 0; i < buffer.Length; i++)
        {
            var x = layout.Points[i].X;
            var hue = Wrap01(x + timeOffset);
            buffer[i] = HsvToRgb(hue, saturation: 1.0, value: 1.0);
        }
    }

    private static double Wrap01(double value)
    {
        var wrapped = value % 1.0;
        return wrapped < 0 ? wrapped + 1.0 : wrapped;
    }

    internal static RgbColor HsvToRgb(double hue, double saturation, double value)
    {
        var h = Wrap01(hue) * 6.0;
        var sector = (int)h;
        var fractional = h - sector;

        var p = value * (1 - saturation);
        var q = value * (1 - (saturation * fractional));
        var t = value * (1 - (saturation * (1 - fractional)));

        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };

        return new RgbColor(ToByte(r), ToByte(g), ToByte(b));
    }

    private static byte ToByte(double channel) => (byte)Math.Clamp(Math.Round(channel * 255), 0, 255);
}
