namespace Porchlight.Core.Lighting.Effects;

/// <summary>The shape of a zone's LEDs, mirrored from OpenRGB.NET's own <c>ZoneType</c> so
/// <see cref="LedLayoutBuilder"/> does not need to reference OpenRGB.NET types directly.</summary>
public enum EffectZoneType
{
    /// <summary>The zone is a single LED.</summary>
    SingleLed,

    /// <summary>The zone is a line of LEDs, like an LED strip.</summary>
    Linear,

    /// <summary>The zone is a 2D grid of LEDs, like a keyboard.</summary>
    Matrix,
}
