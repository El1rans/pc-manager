namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// An expanding ring of light from each recent key press's LED grid position, fading out as it
/// grows - see docs/specs/11-led-effects.md. Driven entirely by <see cref="IEffectContext.RecentKeyPresses"/>
/// (from a phase 2 <see cref="IKeyPressSource"/>); with the phase 1 default
/// <see cref="NullKeyPressSource"/> there are never any, so this renders nothing. Matrix-only: on a
/// non-matrix layout this renders nothing either.
/// </summary>
public sealed class TypingRippleEffect : IEffect
{
    private static readonly RgbColor RippleColor = new(0x40, 0xFF, 0xC0);

    /// <param name="ringsPerSecond">How fast a ripple's ring radius grows, in grid cells per
    /// second. Must be positive.</param>
    /// <param name="ringWidth">How many cells wide the glowing ring band is.</param>
    /// <param name="maxAgeSeconds">How long a press keeps rippling before it stops contributing at
    /// all. Must be positive.</param>
    public TypingRippleEffect(double ringsPerSecond = 6.0, double ringWidth = 1.5, double maxAgeSeconds = 1.5)
    {
        if (ringsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ringsPerSecond), ringsPerSecond, "Must be positive.");
        }

        if (ringWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ringWidth), ringWidth, "Must be positive.");
        }

        if (maxAgeSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAgeSeconds), maxAgeSeconds, "Must be positive.");
        }

        RingsPerSecond = ringsPerSecond;
        RingWidth = ringWidth;
        MaxAgeSeconds = maxAgeSeconds;
    }

    public string Name => "Typing ripple";

    public double RingsPerSecond { get; }

    public double RingWidth { get; }

    public double MaxAgeSeconds { get; }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var layout = frame.Layout;
        var presses = frame.Context.RecentKeyPresses;
        if (!layout.IsMatrix || presses.Count == 0)
        {
            return;
        }

        buffer.Fill(RgbColor.Black);

        foreach (var press in presses)
        {
            var ageSeconds = (frame.Context.UtcNow - press.Timestamp).TotalSeconds;
            if (ageSeconds is < 0 || ageSeconds > MaxAgeSeconds)
            {
                continue;
            }

            var radius = ageSeconds * RingsPerSecond;
            var fade = 1 - (ageSeconds / MaxAgeSeconds);

            for (var i = 0; i < layout.Points.Count; i++)
            {
                var point = layout.Points[i];
                var dx = point.Col!.Value - press.Col;
                var dy = point.Row!.Value - press.Row;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var ringDistance = Math.Abs(distance - radius);
                if (ringDistance > RingWidth)
                {
                    continue;
                }

                var brightness = fade * (1 - (ringDistance / RingWidth));
                var candidate = RippleColor.Scale(brightness);
                buffer[i] = Brighter(buffer[i], candidate);
            }
        }
    }

    /// <summary>Combines overlapping ripples from more than one recent press by keeping the
    /// brighter of the two at each LED, rather than one overwriting the other.</summary>
    private static RgbColor Brighter(RgbColor a, RgbColor b) =>
        (a.R + a.G + a.B) >= (b.R + b.G + b.B) ? a : b;
}
