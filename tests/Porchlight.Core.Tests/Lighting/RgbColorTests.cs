using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class RgbColorTests
{
    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("00FF00", 0, 255, 0)]
    [InlineData("#0000ff", 0, 0, 255)]
    [InlineData("#ABCDEF", 0xAB, 0xCD, 0xEF)]
    public void Parse_ValidHex_ReturnsExpectedChannels(string hex, byte r, byte g, byte b)
    {
        var color = RgbColor.Parse(hex);

        Assert.Equal(new RgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("not a color")]
    [InlineData(null)]
    public void TryParse_InvalidHex_ReturnsFalse(string? hex)
    {
        var result = RgbColor.TryParse(hex, out var color);

        Assert.False(result);
        Assert.Equal(RgbColor.Black, color);
    }

    [Fact]
    public void Parse_InvalidHex_Throws() =>
        Assert.Throws<FormatException>(() => RgbColor.Parse("nope"));

    [Fact]
    public void ToHex_FormatsAsUpperCaseHashRrggbb()
    {
        var color = new RgbColor(0x1A, 0x2B, 0x3C);

        Assert.Equal("#1A2B3C", color.ToHex());
        Assert.Equal("#1A2B3C", color.ToString());
    }

    [Fact]
    public void Scale_FullBrightness_ReturnsSameColor()
    {
        var color = new RgbColor(200, 100, 50);

        Assert.Equal(color, color.Scale(1.0));
    }

    [Fact]
    public void Scale_Zero_ReturnsBlack()
    {
        var color = new RgbColor(200, 100, 50);

        Assert.Equal(RgbColor.Black, color.Scale(0.0));
    }

    [Fact]
    public void Scale_Half_RoundsEachChannel()
    {
        var color = new RgbColor(200, 101, 1);

        var scaled = color.Scale(0.5);

        Assert.Equal(new RgbColor(100, 51, 1), scaled);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Scale_OutOfRangeFactor_Clamps(double factor)
    {
        var color = new RgbColor(200, 100, 50);

        var scaled = color.Scale(factor);

        Assert.Equal(factor < 0 ? RgbColor.Black : color, scaled);
    }
}
