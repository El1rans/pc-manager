using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class RainbowWaveEffectTests
{
    private static LedLayout ThreePointLayout() => new(
        [
            new LedPoint(0, "A", 0.0, 0, null, null),
            new LedPoint(1, "B", 0.5, 0, null, null),
            new LedPoint(2, "C", 1.0, 0, null, null),
        ],
        null, null);

    [Fact]
    public void Render_AtTimeZero_HueFollowsXPosition()
    {
        var effect = new RainbowWaveEffect();
        var layout = ThreePointLayout();
        var frame = new EffectFrame(TimeSpan.Zero, layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[3];

        effect.Render(in frame, buffer);

        // x=0 -> hue 0 (red); x=1 -> hue wraps back to red too (both ends of the rainbow meet).
        Assert.Equal(buffer[0], buffer[2]);
        Assert.NotEqual(buffer[0], buffer[1]);
    }

    [Fact]
    public void Render_Reverse_ScrollsOppositeDirectionFromForward()
    {
        var forward = new RainbowWaveEffect(speed: 1.0, reverse: false);
        var reverse = new RainbowWaveEffect(speed: 1.0, reverse: true);
        var layout = ThreePointLayout();
        var frame = new EffectFrame(TimeSpan.FromSeconds(1), layout, EffectContext.Empty);
        Span<RgbColor> forwardBuffer = stackalloc RgbColor[3];
        Span<RgbColor> reverseBuffer = stackalloc RgbColor[3];

        forward.Render(in frame, forwardBuffer);
        reverse.Render(in frame, reverseBuffer);

        Assert.NotEqual(forwardBuffer[1], reverseBuffer[1]);
    }

    [Fact]
    public void Render_IsDeterministic_SameElapsedProducesSameColors()
    {
        var effect = new RainbowWaveEffect(speed: 1.5);
        var layout = ThreePointLayout();
        var frame = new EffectFrame(TimeSpan.FromSeconds(2.5), layout, EffectContext.Empty);
        Span<RgbColor> first = stackalloc RgbColor[3];
        Span<RgbColor> second = stackalloc RgbColor[3];

        effect.Render(in frame, first);
        effect.Render(in frame, second);

        Assert.True(first.SequenceEqual(second));
    }

    [Fact]
    public void Constructor_NonPositiveSpeed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RainbowWaveEffect(speed: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RainbowWaveEffect(speed: -1));
    }

    [Theory]
    [InlineData(0.0, 255, 0, 0)]
    [InlineData(1.0 / 3.0, 0, 255, 0)]
    [InlineData(2.0 / 3.0, 0, 0, 255)]
    public void HsvToRgb_PrimaryHues_MapToExpectedChannels(double hue, byte r, byte g, byte b)
    {
        var color = RainbowWaveEffect.HsvToRgb(hue, saturation: 1.0, value: 1.0);

        Assert.Equal(new RgbColor(r, g, b), color);
    }
}
