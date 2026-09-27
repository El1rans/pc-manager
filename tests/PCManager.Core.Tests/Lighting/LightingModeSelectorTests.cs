using PCManager.Core.Lighting;
using Xunit;

namespace PCManager.Core.Tests.Lighting;

public sealed class LightingModeSelectorTests
{
    [Fact]
    public void SelectColorMode_DirectAvailable_PrefersDirectOverStatic()
    {
        RgbMode[] modes = [new(0, "Static"), new(1, "Direct"), new(2, "Rainbow")];

        var selected = LightingModeSelector.SelectColorMode(modes);

        Assert.NotNull(selected);
        Assert.Equal("Direct", selected.Name);
    }

    [Fact]
    public void SelectColorMode_NoDirect_FallsBackToStatic()
    {
        RgbMode[] modes = [new(0, "Rainbow"), new(1, "Static")];

        var selected = LightingModeSelector.SelectColorMode(modes);

        Assert.NotNull(selected);
        Assert.Equal("Static", selected.Name);
    }

    [Fact]
    public void SelectColorMode_NeitherAvailable_ReturnsNull()
    {
        RgbMode[] modes = [new(0, "Rainbow"), new(1, "Breathing")];

        var selected = LightingModeSelector.SelectColorMode(modes);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectColorMode_IsCaseInsensitive()
    {
        RgbMode[] modes = [new(0, "direct")];

        var selected = LightingModeSelector.SelectColorMode(modes);

        Assert.NotNull(selected);
        Assert.Equal("direct", selected.Name);
    }

    [Fact]
    public void SelectColorMode_EmptyModes_ReturnsNull()
    {
        var selected = LightingModeSelector.SelectColorMode([]);

        Assert.Null(selected);
    }
}
