using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class EffectRegistryTests
{
    [Theory]
    [InlineData("Rainbow wave", typeof(RainbowWaveEffect))]
    [InlineData("Breathing", typeof(BreathingEffect))]
    [InlineData("CPU temperature", typeof(CpuTemperatureEffect))]
    [InlineData("Pac-Man", typeof(PacManEffect))]
    [InlineData("Rain", typeof(RainEffect))]
    [InlineData("Typing ripple", typeof(TypingRippleEffect))]
    public void Create_KnownName_BuildsExpectedType(string name, Type expectedType)
    {
        var effect = EffectRegistry.Create(name);

        Assert.NotNull(effect);
        Assert.IsType(expectedType, effect);
    }

    [Fact]
    public void Create_UnknownName_ReturnsNull()
    {
        Assert.Null(EffectRegistry.Create("Not a real effect"));
    }

    [Fact]
    public void Create_UnparsableSetting_FallsBackToDefault()
    {
        var effect = Assert.IsType<RainbowWaveEffect>(
            EffectRegistry.Create("Rainbow wave", new Dictionary<string, string> { ["speed"] = "not-a-number" }));

        Assert.Equal(1.0, effect.Speed);
    }

    [Fact]
    public void Create_ValidSettings_AreApplied()
    {
        var effect = Assert.IsType<BreathingEffect>(
            EffectRegistry.Create(
                "Breathing",
                new Dictionary<string, string> { ["speed"] = "2.5", ["color"] = "#FF00FF", ["minBrightness"] = "0.2" }));

        Assert.Equal(2.5, effect.Speed);
        Assert.Equal("#FF00FF", effect.Color.ToHex());
        Assert.Equal(0.2, effect.MinBrightness);
    }

    [Fact]
    public void CreateWithOverlay_UnknownName_ReturnsNull()
    {
        Assert.Null(EffectRegistry.CreateWithOverlay("Not a real effect", settings: null, withUpdatesAlert: true));
    }

    [Theory]
    [InlineData(false, typeof(RainbowWaveEffect))] // no alert: the base effect
    [InlineData(true, typeof(UpdatesAlertEffect))] // alert: wrapped in the overlay
    public void CreateWithOverlay_KnownName_ReturnsBaseOrWrappedEffectPerUpdatesAlert(bool withUpdatesAlert, Type expectedType)
    {
        var effect = EffectRegistry.CreateWithOverlay("Rainbow wave", settings: null, withUpdatesAlert: withUpdatesAlert);

        Assert.IsType(expectedType, effect);
    }

    private static readonly string[] ExpectedNames =
        ["Rainbow wave", "Breathing", "CPU temperature", "Pac-Man", "Rain", "Typing ripple", "Custom animation"];

    [Fact]
    public void Names_ContainsEveryRegisteredEffect() => Assert.Equal(ExpectedNames, EffectRegistry.Names);
}
