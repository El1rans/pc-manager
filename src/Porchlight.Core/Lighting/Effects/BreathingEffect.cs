namespace Porchlight.Core.Lighting.Effects;

/// <summary>A single color that fades smoothly in and out ("breathes") - see
/// docs/specs/11-led-effects.md.</summary>
public sealed class BreathingEffect : IEffect
{
    /// <summary>How many full breathe cycles per second at <c>Speed = 1</c>.</summary>
    private const double BaseCyclesPerSecond = 0.25;

    /// <param name="color">The color that breathes.</param>
    /// <param name="speed">Scales the breathing rate; 1.0 is the default pace. Must be
    /// positive.</param>
    /// <param name="minBrightness">The dimmest point of the cycle, 0..1. Keeps the effect from
    /// ever going fully black when above 0.</param>
    public BreathingEffect(RgbColor color, double speed = 1.0, double minBrightness = 0.0)
    {
        if (speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        if (minBrightness is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minBrightness), minBrightness, "Must be between 0 and 1.");
        }

        Color = color;
        Speed = speed;
        MinBrightness = minBrightness;
    }

    public string Name => "Breathing";

    public RgbColor Color { get; }

    public double Speed { get; }

    public double MinBrightness { get; }

    /// <summary>The current brightness factor (0..1) for the given elapsed time - exposed so tests
    /// can assert the period/phase directly without decoding it back out of scaled colors.</summary>
    public double BrightnessAt(TimeSpan elapsed)
    {
        var phase = elapsed.TotalSeconds * BaseCyclesPerSecond * Speed * 2 * Math.PI;
        var wave = (Math.Sin(phase - (Math.PI / 2)) + 1) / 2; // 0 at t=0, rising smoothly.
        return MinBrightness + ((1 - MinBrightness) * wave);
    }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var color = Color.Scale(BrightnessAt(frame.Elapsed));
        buffer.Fill(color);
    }
}
