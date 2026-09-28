namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// A flat color mapped from the CPU temperature: blue (cold) through green and amber to red (hot).
/// Smooths the raw reading with a short exponential moving average so a single noisy sample does
/// not visibly flicker the LEDs - see docs/specs/11-led-effects.md.
/// </summary>
/// <remarks>
/// The only effect that keeps state across frames (the smoothed temperature and when it was last
/// updated), needed for the moving average. It stays deterministic for a given sequence of
/// <see cref="Render"/> calls with increasing <c>frame.Elapsed</c>, which is how
/// <see cref="EffectEngine"/> always calls it and how it is unit tested.
/// </remarks>
public sealed class CpuTemperatureEffect : IEffect
{
    private static readonly RgbColor Blue = new(0x20, 0x60, 0xFF);
    private static readonly RgbColor Green = new(0x20, 0xE0, 0x40);
    private static readonly RgbColor Amber = new(0xFF, 0xB0, 0x00);
    private static readonly RgbColor Red = new(0xFF, 0x20, 0x20);

    /// <summary>Color shown when there is no CPU temperature reading (hardware monitoring not
    /// started, or no sensor found) - a neutral, clearly-not-a-temperature dim white.</summary>
    private static readonly RgbColor NeutralFallback = new(0x30, 0x30, 0x30);

    private double? _smoothedC;
    private TimeSpan? _lastElapsed;

    /// <param name="minC">Temperature mapped to pure blue (and below).</param>
    /// <param name="maxC">Temperature mapped to pure red (and above). Must be greater than
    /// <paramref name="minC"/>.</param>
    /// <param name="smoothingSeconds">Exponential moving average time constant, in seconds. 0
    /// disables smoothing (each frame uses the raw reading directly).</param>
    public CpuTemperatureEffect(double minC = 30, double maxC = 85, double smoothingSeconds = 2.0)
    {
        if (maxC <= minC)
        {
            throw new ArgumentOutOfRangeException(nameof(maxC), maxC, "Must be greater than minC.");
        }

        if (smoothingSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothingSeconds), smoothingSeconds, "Cannot be negative.");
        }

        MinC = minC;
        MaxC = maxC;
        SmoothingSeconds = smoothingSeconds;
    }

    public string Name => "CPU temperature";

    public double MinC { get; }

    public double MaxC { get; }

    public double SmoothingSeconds { get; }

    /// <summary>Maps a temperature already normalized to <c>[minC, maxC]</c> onto the blue -> green
    /// -> amber -> red gradient. Pure; used directly by <see cref="Render"/> and by tests that want
    /// to check the gradient rule without exercising the smoothing.</summary>
    public static RgbColor ColorForTemperature(double celsius, double minC, double maxC)
    {
        var t = maxC <= minC ? 0 : Math.Clamp((celsius - minC) / (maxC - minC), 0, 1);

        // Three equal-length segments: blue->green, green->amber, amber->red.
        if (t < 1.0 / 3)
        {
            return Lerp(Blue, Green, t / (1.0 / 3));
        }

        if (t < 2.0 / 3)
        {
            return Lerp(Green, Amber, (t - (1.0 / 3)) / (1.0 / 3));
        }

        return Lerp(Amber, Red, (t - (2.0 / 3)) / (1.0 / 3));
    }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var reading = frame.Context.CpuTemperatureCelsius;
        if (reading is not { } celsius)
        {
            buffer.Fill(NeutralFallback);
            return;
        }

        var smoothed = Smooth(celsius, frame.Elapsed);
        buffer.Fill(ColorForTemperature(smoothed, MinC, MaxC));
    }

    private double Smooth(double celsius, TimeSpan elapsed)
    {
        if (_smoothedC is not { } previous || _lastElapsed is not { } lastElapsed || SmoothingSeconds <= 0)
        {
            _smoothedC = celsius;
            _lastElapsed = elapsed;
            return celsius;
        }

        var dt = Math.Max(0, (elapsed - lastElapsed).TotalSeconds);
        var alpha = 1 - Math.Exp(-dt / SmoothingSeconds);
        var smoothed = previous + (alpha * (celsius - previous));

        _smoothedC = smoothed;
        _lastElapsed = elapsed;
        return smoothed;
    }

    private static RgbColor Lerp(RgbColor from, RgbColor to, double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return new RgbColor(
            LerpChannel(from.R, to.R, clamped),
            LerpChannel(from.G, to.G, clamped),
            LerpChannel(from.B, to.B, clamped));
    }

    private static byte LerpChannel(byte from, byte to, double t) =>
        (byte)Math.Clamp(Math.Round(from + ((to - from) * t)), 0, 255);
}
