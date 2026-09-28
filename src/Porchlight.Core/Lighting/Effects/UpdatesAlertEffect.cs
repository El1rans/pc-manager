namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Decorator that renders another effect and then, only while
/// <see cref="IEffectContext.PendingUpdateCount"/> is greater than zero, pulses amber on top of it -
/// the top row for a matrix layout, or the whole layout otherwise - see
/// docs/specs/11-led-effects.md.
/// </summary>
public sealed class UpdatesAlertEffect : IEffect
{
    private static readonly RgbColor AlertColor = new(0xFF, 0xA0, 0x00);

    /// <summary>How many full pulse cycles per second.</summary>
    private const double PulseCyclesPerSecond = 1.0;

    private readonly IEffect _inner;

    public UpdatesAlertEffect(IEffect inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public string Name => $"{_inner.Name} + updates alert";

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        _inner.Render(in frame, buffer);

        if (frame.Context.PendingUpdateCount <= 0)
        {
            return;
        }

        var pulse = PulseIntensityAt(frame.Elapsed);
        var layout = frame.Layout;

        for (var i = 0; i < buffer.Length; i++)
        {
            if (!AffectsPoint(layout, i))
            {
                continue;
            }

            buffer[i] = Blend(buffer[i], AlertColor, pulse);
        }
    }

    /// <summary>The pulse's brightness factor (0..1) at the given elapsed time - exposed for
    /// tests.</summary>
    public static double PulseIntensityAt(TimeSpan elapsed)
    {
        var phase = elapsed.TotalSeconds * PulseCyclesPerSecond * 2 * Math.PI;
        return (Math.Sin(phase) + 1) / 2;
    }

    private static bool AffectsPoint(LedLayout layout, int index) =>
        !layout.IsMatrix || layout.Points[index].Row == 0;

    private static RgbColor Blend(RgbColor baseColor, RgbColor overlay, double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return new RgbColor(
            BlendChannel(baseColor.R, overlay.R, clamped),
            BlendChannel(baseColor.G, overlay.G, clamped),
            BlendChannel(baseColor.B, overlay.B, clamped));
    }

    private static byte BlendChannel(byte baseChannel, byte overlayChannel, double t) =>
        (byte)Math.Clamp(Math.Round(baseChannel + ((overlayChannel - baseChannel) * t)), 0, 255);
}
