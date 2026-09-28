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

    [Fact]
    public void SelectDirectMode_DirectModeNotPerLed_ReturnsNull()
    {
        IReadOnlyList<EffectModeInfo> modes = [new EffectModeInfo(0, "Direct", IsPerLed: false)];

        Assert.Null(EffectModeSelector.SelectDirectMode(modes));
    }

    [Fact]
    public void SelectDirectMode_NoDirectMode_ReturnsNull()
    {
        IReadOnlyList<EffectModeInfo> modes = [new EffectModeInfo(0, "Static", IsPerLed: false)];

        Assert.Null(EffectModeSelector.SelectDirectMode(modes));
    }

    [Fact]
    public void SelectDirectMode_IsCaseInsensitive()
    {
        IReadOnlyList<EffectModeInfo> modes = [new EffectModeInfo(0, "DIRECT", IsPerLed: true)];

        Assert.NotNull(EffectModeSelector.SelectDirectMode(modes));
    }
}
