using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class EffectModeSelectorTests
{
    [Fact]
    public void SelectDirectMode_HasPerLedDirectMode_ReturnsIt()
    {
        IReadOnlyList<EffectModeInfo> modes =
        [
            new EffectModeInfo(0, "Static", IsPerLed: false),
            new EffectModeInfo(1, "Direct", IsPerLed: true),
        ];

        var selected = EffectModeSelector.SelectDirectMode(modes);

        Assert.NotNull(selected);
        Assert.Equal(1, selected.Index);
    }

    [Theory]
    [InlineData("Direct")] // a Direct mode that is not per-LED
    [InlineData("Static")] // no Direct mode at all
    public void SelectDirectMode_NoPerLedDirectMode_ReturnsNull(string modeName)
    {
        IReadOnlyList<EffectModeInfo> modes = [new EffectModeInfo(0, modeName, IsPerLed: false)];

        Assert.Null(EffectModeSelector.SelectDirectMode(modes));
    }

    [Fact]
    public void SelectDirectMode_IsCaseInsensitive()
    {
        IReadOnlyList<EffectModeInfo> modes = [new EffectModeInfo(0, "DIRECT", IsPerLed: true)];

        Assert.NotNull(EffectModeSelector.SelectDirectMode(modes));
    }
}
