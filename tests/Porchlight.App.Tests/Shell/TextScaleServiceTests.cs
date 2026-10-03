using System.Windows;
using System.Windows.Media;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Shell;

public sealed class TextScaleServiceTests
{
    [Theory]
    [InlineData(TextSize.Normal, TextSizeScale.NormalFactor)]
    [InlineData(TextSize.Large, TextSizeScale.LargeFactor)]
    [InlineData(TextSize.ExtraLarge, TextSizeScale.ExtraLargeFactor)]
    public void Apply_PublishesTheMatchingScaleTransform(TextSize size, double expected)
    {
        var resources = new ResourceDictionary();

        TextScaleService.Apply(size, resources);

        var transform = Assert.IsType<ScaleTransform>(resources[TextScaleService.TransformResourceKey]);
        Assert.Equal(expected, transform.ScaleX);
        Assert.Equal(expected, transform.ScaleY);
    }

    [Fact]
    public void Apply_ReplacesThePreviousTransform()
    {
        var resources = new ResourceDictionary();

        TextScaleService.Apply(TextSize.ExtraLarge, resources);
        TextScaleService.Apply(TextSize.Normal, resources);

        Assert.Equal(TextSizeScale.NormalFactor, ((ScaleTransform)resources[TextScaleService.TransformResourceKey]).ScaleX);
    }

    [Fact]
    public void Scales_GetBiggerWithEachStep()
    {
        Assert.True(TextSizeScale.FactorFor(TextSize.Normal) < TextSizeScale.FactorFor(TextSize.Large));
        Assert.True(TextSizeScale.FactorFor(TextSize.Large) < TextSizeScale.FactorFor(TextSize.ExtraLarge));
    }
}
