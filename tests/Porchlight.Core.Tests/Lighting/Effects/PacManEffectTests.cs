using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class PacManEffectTests
{
    [Fact]
    public void BuildPath_Boustrophedon_AlternatesRowDirection()
    {
        var layout = MakeGrid(width: 3, height: 2);

        var path = PacManEffect.BuildPath(layout);

        var cells = path.Select(i => (layout.Points[i].Col, layout.Points[i].Row)).ToList();
        Assert.Equal(
            [(0, 0), (1, 0), (2, 0), (2, 1), (1, 1), (0, 1)],
            cells);
    }

    [Fact]
    public void BuildPath_NonMatrixLayout_ReturnsEmpty()
    {
        LedLayout layout = new([new LedPoint(0, "A", 0.5, 0.5, null, null)], null, null);

        var path = PacManEffect.BuildPath(layout);

        Assert.Empty(path);
    }

    [Fact]
    public void Render_AtTimeT_KeysAlreadyPassedAreDarkAndKeysAheadAreLit()
    {
        var layout = MakeGrid(width: 5, height: 1);
        var effect = new PacManEffect(cellsPerSecond: 1.0, ghostLeadCells: 0);
        var frame = new EffectFrame(TimeSpan.FromSeconds(2.5), layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[layout.Count];

        effect.Render(in frame, buffer);

        // At t=2.5s, 1 cell/sec => Pac-Man is on cell index 2 (0-based), cells 0 and 1 eaten.
        Assert.Equal(RgbColor.Black, buffer[0]);
        Assert.Equal(RgbColor.Black, buffer[1]);
        Assert.NotEqual(RgbColor.Black, buffer[2]); // Pac-Man himself.
        Assert.NotEqual(RgbColor.Black, buffer[3]); // Ahead - still a dot.
        Assert.NotEqual(RgbColor.Black, buffer[4]);
    }

    [Fact]
    public void Render_OnCompletion_PathRefillsAndLoops()
    {
        var layout = MakeGrid(width: 5, height: 1);
        var effect = new PacManEffect(cellsPerSecond: 1.0, ghostLeadCells: 0);

        Span<RgbColor> bufferStart = stackalloc RgbColor[layout.Count];
        var frameStart = new EffectFrame(TimeSpan.Zero, layout, EffectContext.Empty);
        effect.Render(in frameStart, bufferStart);

        Span<RgbColor> bufferAfterFullLoop = stackalloc RgbColor[layout.Count];
        var frameAfterFullLoop = new EffectFrame(TimeSpan.FromSeconds(5), layout, EffectContext.Empty);
        effect.Render(in frameAfterFullLoop, bufferAfterFullLoop);

        // 5 cells at 1 cell/sec means t=5s wraps back to the same position as t=0 - everything is
        // "refilled" (no cell stuck permanently dark).
        for (var i = 0; i < layout.Count; i++)
        {
            Assert.Equal(bufferStart[i], bufferAfterFullLoop[i]);
        }
    }

    [Fact]
    public void Render_GhostFleesAheadOfPacMan()
    {
        var layout = MakeGrid(width: 6, height: 1);
        var effect = new PacManEffect(cellsPerSecond: 1.0, ghostLeadCells: 2);
        var frame = new EffectFrame(TimeSpan.FromSeconds(1.5), layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[layout.Count];

        effect.Render(in frame, buffer);

        // Pac-Man on cell 1, so the ghost is 2 cells ahead on cell 3 - never behind him.
        var ghost = buffer[3];
        Assert.NotEqual(buffer[1], ghost);
        Assert.NotEqual(buffer[2], ghost);
        Assert.NotEqual(buffer[0], ghost);
        Assert.Equal(1, CountOf(buffer, ghost));
    }

    [Fact]
    public void Render_NearTheEnd_GhostIsCorneredOnTheLastCell()
    {
        var layout = MakeGrid(width: 6, height: 1);
        var effect = new PacManEffect(cellsPerSecond: 1.0, ghostLeadCells: 3);
        var chased = new EffectFrame(TimeSpan.FromSeconds(1), layout, EffectContext.Empty);
        var cornered = new EffectFrame(TimeSpan.FromSeconds(4), layout, EffectContext.Empty);
        Span<RgbColor> chasedBuffer = stackalloc RgbColor[layout.Count];
        Span<RgbColor> corneredBuffer = stackalloc RgbColor[layout.Count];

        effect.Render(in chased, chasedBuffer);
        effect.Render(in cornered, corneredBuffer);

        // Pac-Man on cell 4 with a lead of 3 would put the ghost past the end - it stays on cell 5.
        var ghost = chasedBuffer[4];
        Assert.Equal(ghost, corneredBuffer[5]);
    }

    private static int CountOf(ReadOnlySpan<RgbColor> buffer, RgbColor color)
    {
        var count = 0;
        foreach (var c in buffer)
        {
            if (c == color)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Render_NonMatrixLayout_RendersNothing()
    {
        LedLayout layout = new([new LedPoint(0, "A", 0.5, 0.5, null, null)], null, null);
        var effect = new PacManEffect();
        var frame = new EffectFrame(TimeSpan.FromSeconds(1), layout, EffectContext.Empty);
        Span<RgbColor> buffer = stackalloc RgbColor[1];

        effect.Render(in frame, buffer);

        Assert.Equal(default, buffer[0]);
    }

    private static LedLayout MakeGrid(int width, int height)
    {
        var points = new List<LedPoint>();
        var index = 0;
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var x = width == 1 ? 0 : (double)col / (width - 1);
                var y = height == 1 ? 0 : (double)row / (height - 1);
                points.Add(new LedPoint(index++, $"({col},{row})", x, y, col, row));
            }
        }

        return new LedLayout(points, width, height);
    }
}
