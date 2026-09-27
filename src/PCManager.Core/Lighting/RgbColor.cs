using System.Globalization;

namespace PCManager.Core.Lighting;

/// <summary>
/// An RGB color, parsed from and formatted as <c>#RRGGBB</c>. Immutable; <see cref="Scale"/>
/// returns a new, brightness-scaled instance rather than mutating this one.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static readonly RgbColor Black = new(0, 0, 0);

    public static readonly RgbColor White = new(255, 255, 255);

    /// <summary>Parses a <c>#RRGGBB</c> or <c>RRGGBB</c> hex string.</summary>
    /// <exception cref="FormatException">Not a valid 6-digit hex color.</exception>
    public static RgbColor Parse(string hex)
    {
        if (!TryParse(hex, out var color))
        {
            throw new FormatException($"'{hex}' is not a valid #RRGGBB color.");
        }

        return color;
    }

    /// <summary>Tries to parse a <c>#RRGGBB</c> or <c>RRGGBB</c> hex string.</summary>
    public static bool TryParse(string? hex, out RgbColor color)
    {
        color = Black;

        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = new RgbColor(r, g, b);
        return true;
    }

    /// <summary>Formats as <c>#RRGGBB</c> (upper-case hex digits).</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    /// <summary>Scales every channel by <paramref name="factor"/>, clamped to [0, 1] and rounded to
    /// the nearest byte. Used for the brightness slider: 1.0 leaves the color unchanged, 0.0 turns
    /// it fully off.</summary>
    public RgbColor Scale(double factor)
    {
        var clamped = Math.Clamp(factor, 0d, 1d);
        return new RgbColor(ScaleChannel(R, clamped), ScaleChannel(G, clamped), ScaleChannel(B, clamped));
    }

    private static byte ScaleChannel(byte channel, double factor) =>
        (byte)Math.Clamp(Math.Round(channel * factor, MidpointRounding.AwayFromZero), 0, 255);
}
