using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class BreathingEffectTests
{
    [Fact]
    public void BrightnessAt_Zero_StartsAtMinBrightness()
    {
        var effect = new BreathingEffect(RgbColor.White, speed: 1.0, minBrightness: 0.1);

        var brightness = effect.BrightnessAt(TimeSpan.Zero);

        Assert.Equal(0.1, brightness, precision: 6);
    }

    [Fact]
    public void BrightnessAt_IsPeriodic_MatchesConfiguredSpeed()
    {
        // At the default rate (0.25 cycles/sec), a full period is 4 seconds at Speed = 1.
        var effect = new BreathingEffect(RgbColor.White, speed: 1.0);

        var atStart = effect.BrightnessAt(TimeSpan.Zero);
        var atOnePeriod = effect.BrightnessAt(TimeSpan.FromSeconds(4));
        var atTwoPeriods = effect.BrightnessAt(TimeSpan.FromSeconds(8));

        Assert.Equal(atStart, atOnePeriod, precision: 6);
        Assert.Equal(atStart, atTwoPeriods, precision: 6);
    }

    [Fact]
    public void BrightnessAt_DoubleSpeed_HalvesThePeriod()
    {
        var normal = new BreathingEffect(RgbColor.White, speed: 1.0);
        var fast = new BreathingEffect(RgbColor.White, speed: 2.0);

        var normalAtTwoSeconds = normal.BrightnessAt(TimeSpan.FromSeconds(2));
        var fastAtOneSecond = fast.BrightnessAt(TimeSpan.FromSeconds(1));

        Assert.Equal(normalAtTwoSeconds, fastAtOneSecond, precision: 6);
    }

    [Fact]
    public void BrightnessAt_ReachesFullBrightnessAtHalfPeriod()
    {
        var effect = new BreathingEffect(RgbColor.White, speed: 1.0);

        // Half of a 4-second period = 2 seconds - the peak of the raised-cosine breathe.
        var brightness = effect.BrightnessAt(TimeSpan.FromSeconds(2));

        Assert.Equal(1.0, brightness, precision: 6);
    }

    [Fact]
    public void Render_FillsEveryLedWithScaledColor()
    {
        var effect = new BreathingEffect(new RgbColor(200, 100, 50), speed: 1.0);
        LedLayout layout = new(
            [new LedPoint(0, "A", 0, 0, null, null), new LedPoint(1, "B", 1, 0, null, null)], null, null);
        var frame = new EffectFrame(TimeSpan.FromSeconds(1), layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[2];

        effect.Render(in frame, buffer);

        Assert.Equal(buffer[0], buffer[1]);
    }
}
