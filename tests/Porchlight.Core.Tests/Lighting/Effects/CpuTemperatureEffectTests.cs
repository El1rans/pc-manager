using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class CpuTemperatureEffectTests
{
    [Fact]
    public void ColorForTemperature_AtMin_IsBlue()
    {
        var color = CpuTemperatureEffect.ColorForTemperature(30, minC: 30, maxC: 90);

        Assert.True(color.B > color.R);
        Assert.True(color.B > color.G);
    }

    [Fact]
    public void ColorForTemperature_AtMax_IsRed()
    {
        var color = CpuTemperatureEffect.ColorForTemperature(90, minC: 30, maxC: 90);

        Assert.True(color.R > color.G);
        Assert.True(color.R > color.B);
        Assert.True(color.G < 60);
    }

    [Fact]
    public void ColorForTemperature_AtMidpoint_IsBetweenGreenAndAmber()
    {
        // Midpoint (t = 0.5) falls in the green -> amber segment.
        var color = CpuTemperatureEffect.ColorForTemperature(60, minC: 30, maxC: 90);

        Assert.True(color.G > 100);
        Assert.True(color.R > 50);
    }

    [Fact]
    public void ColorForTemperature_BelowMin_ClampsToBlue()
    {
        var atMin = CpuTemperatureEffect.ColorForTemperature(30, minC: 30, maxC: 90);
        var belowMin = CpuTemperatureEffect.ColorForTemperature(-10, minC: 30, maxC: 90);

        Assert.Equal(atMin, belowMin);
    }

    [Fact]
    public void Render_NoTemperatureReading_UsesNeutralFallback()
    {
        var effect = new CpuTemperatureEffect();
        var layout = SinglePointLayout();
        var context = new EffectContext(DateTimeOffset.UnixEpoch, CpuTemperatureCelsius: null, 0, []);
        var frame = new EffectFrame(TimeSpan.Zero, layout, context);
        Span<RgbColor> buffer = stackalloc RgbColor[1];

        effect.Render(in frame, buffer);

        // Neutral fallback is a flat gray - not saturated toward any single channel.
        Assert.Equal(buffer[0].R, buffer[0].G);
        Assert.Equal(buffer[0].G, buffer[0].B);
    }

    [Fact]
    public void Render_SameInputsTwice_IsDeterministic()
    {
        var layout = SinglePointLayout();
        var context = new EffectContext(DateTimeOffset.UnixEpoch, 70, 0, []);
        var frame = new EffectFrame(TimeSpan.FromSeconds(1), layout, context);

        var effectA = new CpuTemperatureEffect(smoothingSeconds: 0);
        var effectB = new CpuTemperatureEffect(smoothingSeconds: 0);
        Span<RgbColor> bufferA = stackalloc RgbColor[1];
        Span<RgbColor> bufferB = stackalloc RgbColor[1];

        effectA.Render(in frame, bufferA);
        effectB.Render(in frame, bufferB);

        Assert.Equal(bufferA[0], bufferB[0]);
    }

    private static LedLayout SinglePointLayout() =>
        new([new LedPoint(0, "LED 0", 0.5, 0.5, null, null)], null, null);
}
