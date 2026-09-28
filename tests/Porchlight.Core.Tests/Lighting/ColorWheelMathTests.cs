using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class ColorWheelMathTests
{
    [Fact]
    public void PointToHueSaturation_Center_IsZeroSaturation()
    {
        var (hue, saturation) = ColorWheelMath.PointToHueSaturation(0, 0);

        Assert.Equal(0, hue);
        Assert.Equal(0, saturation);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 90)]
    [InlineData(-1, 0, 180)]
    [InlineData(0, -1, 270)]
    public void PointToHueSaturation_CardinalPoints_MapToExpectedHue(double x, double y, double expectedHue)
    {
        var (hue, saturation) = ColorWheelMath.PointToHueSaturation(x, y);

        Assert.Equal(expectedHue, hue, 3);
        Assert.Equal(1, saturation, 3);
    }

    [Fact]
    public void PointToHueSaturation_OutsideDisc_ClampsSaturationToOne()
    {
        var (_, saturation) = ColorWheelMath.PointToHueSaturation(3, 4);

        Assert.Equal(1, saturation);
    }

    [Fact]
    public void PointToHueSaturation_NeverReturnsNegativeHue()
    {
        var (hue, _) = ColorWheelMath.PointToHueSaturation(0, -0.5);

        Assert.InRange(hue, 0, 360);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(90, 0.5)]
    [InlineData(180, 1)]
    [InlineData(270, 0.25)]
    [InlineData(359, 0.8)]
    public void RoundTrip_HueSaturationToPointAndBack_IsStable(double hue, double saturation)
    {
        var (x, y) = ColorWheelMath.HueSaturationToPoint(hue, saturation);

        var (roundTrippedHue, roundTrippedSaturation) = ColorWheelMath.PointToHueSaturation(x, y);

        Assert.Equal(hue, roundTrippedHue, 6);
        Assert.Equal(saturation, roundTrippedSaturation, 6);
    }

    [Fact]
    public void HueSaturationToPoint_ClampsSaturationAboveOne()
    {
        var (x, y) = ColorWheelMath.HueSaturationToPoint(0, 5);

        Assert.Equal(1, x, 6);
        Assert.Equal(0, y, 6);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(0.6, 0.6, true)]
    [InlineData(0.8, 0.8, false)]
    [InlineData(2, 0, false)]
    public void IsInsideDisc_ChecksUnitRadius(double x, double y, bool expected)
    {
        Assert.Equal(expected, ColorWheelMath.IsInsideDisc(x, y));
    }
}
