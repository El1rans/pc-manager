using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class HsvColorTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void FromRgb_KnownColors_ProducesExpectedHsv(byte r, byte g, byte b, double expectedHue, double expectedSaturation, double expectedValue)
    {
        var hsv = HsvColor.FromRgb(new RgbColor(r, g, b));

        Assert.Equal(expectedHue, hsv.Hue, 3);
        Assert.Equal(expectedSaturation, hsv.Saturation, 3);
        Assert.Equal(expectedValue, hsv.Value, 3);
    }

    [Theory]
    [InlineData(0, 1, 1, 255, 0, 0)]
    [InlineData(120, 1, 1, 0, 255, 0)]
    [InlineData(240, 1, 1, 0, 0, 255)]
    [InlineData(0, 0, 1, 255, 255, 255)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void ToRgb_KnownHsv_ProducesExpectedRgb(double hue, double saturation, double value, byte expectedR, byte expectedG, byte expectedB)
    {
        var rgb = new HsvColor(hue, saturation, value).ToRgb();

        Assert.Equal(expectedR, rgb.R);
        Assert.Equal(expectedG, rgb.G);
        Assert.Equal(expectedB, rgb.B);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(30, 0.5, 0.7)]
    [InlineData(90, 1.0, 0.25)]
    [InlineData(180, 0.33, 0.9)]
    [InlineData(270, 0.8, 0.4)]
    [InlineData(359, 0.1, 0.1)]
    public void RoundTrip_RgbToHsvToRgb_IsStable(double hue, double saturation, double value)
    {
        var original = new HsvColor(hue, saturation, value).ToRgb();

        var roundTripped = HsvColor.FromRgb(original).ToRgb();

        // Round-tripping through a byte-quantized RGB color must be exact once already quantized -
        // this is what the wheel + value slider actually store and send to OpenRGB.
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void ToRgb_HueOutsideZeroTo360_NormalizesFirst()
    {
        var overHue = new HsvColor(360 + 120, 1, 1).ToRgb();
        var underHue = new HsvColor(-240, 1, 1).ToRgb();
        var expected = new HsvColor(120, 1, 1).ToRgb();

        Assert.Equal(expected, overHue);
        Assert.Equal(expected, underHue);
    }

    [Fact]
    public void ToRgb_OutOfRangeSaturationAndValue_Clamps()
    {
        var clampedHigh = new HsvColor(0, 2, 2).ToRgb();
        var clampedLow = new HsvColor(0, -1, -1).ToRgb();

        Assert.Equal(new RgbColor(255, 0, 0), clampedHigh);
        Assert.Equal(RgbColor.Black, clampedLow);
    }

    [Fact]
    public void Normalized_ClampsAndWrapsComponents()
    {
        var normalized = new HsvColor(-30, 2, -1).Normalized();

        Assert.Equal(330, normalized.Hue, 3);
        Assert.Equal(1, normalized.Saturation);
        Assert.Equal(0, normalized.Value);
    }
}
