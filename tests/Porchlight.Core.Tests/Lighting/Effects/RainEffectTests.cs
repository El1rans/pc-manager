using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class RainEffectTests
{
    private static LedLayout MatrixLayout(int width, int height)
    {
        var points = new List<LedPoint>();
        var index = 0;
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var x = width == 1 ? 0d : (double)col / (width - 1);
                var y = height == 1 ? 0d : (double)row / (height - 1);
                points.Add(new LedPoint(index++, $"{col},{row}", x, y, col, row));
            }
        }

        return new LedLayout(points, width, height);
    }

    [Fact]
    public void Render_NonMatrixLayout_RendersNothing()
    {
        var effect = new RainEffect();
        LedLayout linear = new([new LedPoint(0, "A", 0, 0.5, null, null)], null, null);
        var frame = new EffectFrame(TimeSpan.FromSeconds(1), linear, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[1];

        effect.Render(in frame, buffer);

        Assert.Equal(RgbColor.Black, buffer[0]);
    }

    [Fact]
    public void Render_MatrixLayout_LeavesSomeCellsDarkAndSomeLit()
    {
        var effect = new RainEffect(rowsPerSecond: 8, trailLength: 4);
        var layout = MatrixLayout(width: 4, height: 6);
        var frame = new EffectFrame(TimeSpan.FromSeconds(0.3), layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[layout.Count];

        effect.Render(in frame, buffer);

        Assert.Contains(buffer.ToArray(), c => c != RgbColor.Black);
    }

    [Fact]
    public void Render_IsDeterministic_SameElapsedProducesSameColors()
    {
        var effect = new RainEffect();
        var layout = MatrixLayout(width: 3, height: 3);
        var frame = new EffectFrame(TimeSpan.FromSeconds(1.7), layout, EffectContext.Empty);
        Span<RgbColor> first = stackalloc RgbColor[layout.Count];
        Span<RgbColor> second = stackalloc RgbColor[layout.Count];

        effect.Render(in frame, first);
        effect.Render(in frame, second);

        Assert.True(first.SequenceEqual(second));
    }

    [Fact]
    public void Constructor_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RainEffect(rowsPerSecond: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RainEffect(trailLength: 0));
    }

    [Fact]
    public void HeadRowAt_AdvancesOverTime()
    {
        var effect = new RainEffect(rowsPerSecond: 10, trailLength: 4);

        var atZero = effect.HeadRowAt(column: 0, rowCount: 10, TimeSpan.Zero);
        var atOneSecond = effect.HeadRowAt(column: 0, rowCount: 10, TimeSpan.FromSeconds(1));

        Assert.NotEqual(atZero, atOneSecond);
    }
}
