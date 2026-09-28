namespace Porchlight.Core.Lighting.Effects;

/// <summary>One LED's position within a <see cref="LedLayout"/>.</summary>
/// <param name="DeviceLedIndex">The LED's index within its device's full LED/color array - what
/// <see cref="EffectEngine"/> writes a rendered color into before calling
/// <see cref="IEffectDeviceClient.UpdateLeds"/>.</param>
/// <param name="Name">Display name (e.g. "Key: A"), or a generic fallback ("LED 3") when OpenRGB
/// reports none.</param>
/// <param name="X">Normalized horizontal position, 0 (left) to 1 (right).</param>
/// <param name="Y">Normalized vertical position, 0 (top) to 1 (bottom).</param>
/// <param name="Col">Zero-based column within the zone's matrix grid. Only set for a point built
/// from a <see cref="EffectZoneType.Matrix"/> zone.</param>
/// <param name="Row">Zero-based row within the zone's matrix grid. Only set for a point built from
/// a <see cref="EffectZoneType.Matrix"/> zone.</param>
public sealed record LedPoint(int DeviceLedIndex, string Name, double X, double Y, int? Col, int? Row);
