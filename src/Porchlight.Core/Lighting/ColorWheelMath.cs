namespace Porchlight.Core.Lighting;

/// <summary>
/// Pure geometry for <c>ColorWheelPicker</c>'s hue/saturation disc: maps a point in a unit-radius
/// disc centered at the origin (standard Cartesian, +X right, +Y up) to hue/saturation and back.
/// The control converts screen pixel coordinates to/from this unit disc (see its own code-behind);
/// kept here, separate from WPF, so it is unit testable and reusable for the wheel bitmap
/// generation. See docs/specs/05-lighting.md addendum.
/// </summary>
public static class ColorWheelMath
{
    /// <summary>
    /// Maps a point to (hue degrees [0, 360), saturation [0, 1]). A point outside the unit disc
    /// (radius &gt; 1) has its saturation clamped to 1 rather than rejected, so dragging the mouse
    /// past the wheel's edge still lands on the rim instead of doing nothing - matching how most
    /// color pickers handle an out-of-bounds drag.
    /// </summary>
    public static (double Hue, double Saturation) PointToHueSaturation(double x, double y)
    {
        var radius = Math.Sqrt((x * x) + (y * y));
        var saturation = Math.Clamp(radius, 0, 1);

        if (radius <= 0)
        {
            return (0, 0);
        }

        var angleRadians = Math.Atan2(y, x);
        var hue = angleRadians * (180.0 / Math.PI);
        if (hue < 0)
        {
            hue += 360;
        }

        return (hue, saturation);
    }

    /// <summary>Inverse of <see cref="PointToHueSaturation"/>: the point on (or inside) the unit
    /// disc for a given hue/saturation.</summary>
    public static (double X, double Y) HueSaturationToPoint(double hue, double saturation)
    {
        var clampedSaturation = Math.Clamp(saturation, 0, 1);
        var angleRadians = hue * (Math.PI / 180.0);
        return (clampedSaturation * Math.Cos(angleRadians), clampedSaturation * Math.Sin(angleRadians));
    }

    /// <summary>True if the point lies within the unit disc (used to leave the bitmap transparent
    /// outside the wheel, and to know whether a click landed on the wheel at all).</summary>
    public static bool IsInsideDisc(double x, double y) => ((x * x) + (y * y)) <= 1.0;
}
