using System.Text.Json.Serialization;

namespace Porchlight.Core.Settings;

/// <summary>How big Porchlight's own text is. Stored by name so a settings file stays readable.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextSize
{
    /// <summary>The default size (100%).</summary>
    Normal,

    /// <summary>Larger text (125%).</summary>
    Large,

    /// <summary>The largest text (150%).</summary>
    ExtraLarge,
}

/// <summary>Maps a <see cref="TextSize"/> to the scale factor the page area is drawn at.</summary>
public static class TextSizeScale
{
    /// <summary>Scale for <see cref="TextSize.Normal"/>.</summary>
    public const double NormalFactor = 1.0;

    /// <summary>Scale for <see cref="TextSize.Large"/>.</summary>
    public const double LargeFactor = 1.25;

    /// <summary>Scale for <see cref="TextSize.ExtraLarge"/>.</summary>
    public const double ExtraLargeFactor = 1.5;

    public static double FactorFor(TextSize size) => size switch
    {
        TextSize.Large => LargeFactor,
        TextSize.ExtraLarge => ExtraLargeFactor,
        _ => NormalFactor,
    };
}
