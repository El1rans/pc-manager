namespace Porchlight.Core.Lighting;

/// <summary>
/// An HSV (hue/saturation/value) color, used by <c>ColorWheelPicker</c> to translate between the
/// wheel (hue/saturation) and the value slider on one side, and <see cref="RgbColor"/> (what
/// OpenRGB actually wants) on the other. Immutable; pure math, unit tested directly rather than
/// only through the control - see docs/specs/05-lighting.md addendum.
/// </summary>
/// <param name="Hue">Degrees, [0, 360). 0 = red, 120 = green, 240 = blue.</param>
/// <param name="Saturation">[0, 1]. 0 = grayscale, 1 = fully saturated.</param>
/// <param name="Value">[0, 1]. 0 = black, 1 = full brightness for the given hue/saturation.</param>
public readonly record struct HsvColor(double Hue, double Saturation, double Value)
{
    /// <summary>Converts an <see cref="RgbColor"/> to HSV. Gray (R=G=B) has Hue 0 by convention.</summary>
    public static HsvColor FromRgb(RgbColor rgb)
    {
        var r = rgb.R / 255.0;
        var g = rgb.G / 255.0;
        var b = rgb.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue;
        if (delta <= 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max <= 0 ? 0 : delta / max;
        var value = max;

        return new HsvColor(hue, saturation, value);
    }

    /// <summary>Converts to <see cref="RgbColor"/>, rounding each channel to the nearest byte.</summary>
    public RgbColor ToRgb()
    {
        var hue = NormalizeHue(Hue);
        var saturation = Math.Clamp(Saturation, 0, 1);
        var value = Math.Clamp(Value, 0, 1);

        var c = value * saturation;
        var hPrime = hue / 60.0;
        var x = c * (1 - Math.Abs((hPrime % 2) - 1));
        var m = value - c;

        var (r1, g1, b1) = hPrime switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new RgbColor(ToByte(r1 + m), ToByte(g1 + m), ToByte(b1 + m));
    }

    /// <summary>Returns an equivalent color with <see cref="Hue"/> normalized to [0, 360) and
    /// <see cref="Saturation"/>/<see cref="Value"/> clamped to [0, 1].</summary>
    public HsvColor Normalized() => new(NormalizeHue(Hue), Math.Clamp(Saturation, 0, 1), Math.Clamp(Value, 0, 1));

    private static double NormalizeHue(double hue)
    {
        var normalized = hue % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp(Math.Round(channel * 255.0, MidpointRounding.AwayFromZero), 0, 255);
}
