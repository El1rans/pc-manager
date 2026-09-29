using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Lighting.Effects.CustomAnimations;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects.CustomAnimations;

public sealed class CustomAnimationEffectTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    private static readonly LedLayout Strip = new(
        [.. Enumerable.Range(0, 5).Select(i => new LedPoint(i, $"LED {i}", i / 4.0, 0.5, null, null))],
        ColumnCount: null,
        RowCount: null);

    [Fact]
    public void PositionAt_Cut_StepsThroughFramesAndLoops()
    {
        var animation = Parse("""
            { "name": "x", "frameDuration": 1, "frames": [ { "fill": "#FF0000" }, { "fill": "#0000FF" } ] }
            """);

        Assert.Equal((0, 1, 0d), animation.PositionAt(TimeSpan.FromSeconds(0.5)));
        Assert.Equal((1, 0, 0d), animation.PositionAt(TimeSpan.FromSeconds(1.5)));
        Assert.Equal((0, 1, 0d), animation.PositionAt(TimeSpan.FromSeconds(2.5)));
    }

    [Fact]
    public void PositionAt_NoLoop_HoldsLastFrame()
    {
        var animation = Parse("""
            { "name": "x", "loop": false, "transition": "fade", "frameDuration": 1,
              "frames": [ { "fill": "#FF0000" }, { "fill": "#0000FF" } ] }
            """);

        Assert.Equal((1, 1, 0d), animation.PositionAt(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Render_Fade_BlendsHalfwayIntoNextFrame()
    {
        var animation = Parse("""
            { "name": "x", "transition": "fade", "frameDuration": 1,
              "frames": [ { "fill": "#FF0000" }, { "fill": "#0000FF" } ] }
            """);
        var effect = new CustomAnimationEffect(animation);

        var buffer = Render(effect, TimeSpan.FromSeconds(0.5), Strip);

        Assert.All(buffer, c => Assert.Equal(new RgbColor(128, 0, 128), c));
    }

    [Fact]
    public void Render_Speed_ScalesPlayback()
    {
        var animation = Parse("""
            { "name": "x", "frameDuration": 1, "frames": [ { "fill": "#FF0000" }, { "fill": "#0000FF" } ] }
            """);

        var buffer = Render(new CustomAnimationEffect(animation, speed: 2), TimeSpan.FromSeconds(0.75), Strip);

        Assert.All(buffer, c => Assert.Equal(Blue, c));
    }

    [Fact]
    public void Render_Gradient_SpreadsStopsAcrossStrip()
    {
        var animation = Parse("""{ "name": "x", "frames": [ { "gradient": ["#FF0000", "#0000FF"] } ] }""");

        var buffer = Render(new CustomAnimationEffect(animation), TimeSpan.Zero, Strip);

        Assert.Equal(Red, buffer[0]);
        Assert.Equal(new RgbColor(128, 0, 128), buffer[2]);
        Assert.Equal(Blue, buffer[4]);
    }

    [Fact]
    public void Render_RowsMatchingMatrixSize_MapOneCharacterPerKey()
    {
        // A 3x2 matrix with one hole (no LED at column 1, row 1).
        var points = new List<LedPoint>
        {
            new(0, "a", 0, 0, 0, 0), new(1, "b", 0.5, 0, 1, 0), new(2, "c", 1, 0, 2, 0),
            new(3, "d", 0, 1, 0, 1), new(4, "f", 1, 1, 2, 1),
        };
        var layout = new LedLayout(points, 3, 2);
        var animation = Parse("""
            { "name": "x", "palette": { "R": "#FF0000", "B": "#0000FF", ".": "#000000" },
              "frames": [ { "rows": ["R.B", "B.R"] } ] }
            """);

        var buffer = Render(new CustomAnimationEffect(animation), TimeSpan.Zero, layout);

        Assert.Equal([Red, RgbColor.Black, Blue, Blue, Red], buffer);
    }

    [Fact]
    public void Render_TallGridOnStrip_UsesMiddleRow()
    {
        var animation = Parse("""
            { "name": "x", "palette": { "R": "#FF0000", "B": "#0000FF" },
              "frames": [ { "rows": ["RRRRR", "BBBBB", "RRRRR"] } ] }
            """);

        var buffer = Render(new CustomAnimationEffect(animation), TimeSpan.Zero, Strip);

        Assert.All(buffer, c => Assert.Equal(Blue, c));
    }

    [Fact]
    public void Render_IsDeterministic()
    {
        var animation = Parse("""
            { "name": "x", "transition": "fade", "frames": [ { "gradient": ["#FF0000", "#00FF00"] }, { "fill": "#0000FF" } ] }
            """);
        var effect = new CustomAnimationEffect(animation);

        Assert.Equal(
            Render(effect, TimeSpan.FromMilliseconds(137), Strip),
            Render(effect, TimeSpan.FromMilliseconds(137), Strip));
    }

    [Fact]
    public void Constructor_NonPositiveSpeed_Throws()
    {
        var animation = Parse("""{ "name": "x", "frames": [ { "fill": "#FF0000" } ] }""");

        Assert.Throws<ArgumentOutOfRangeException>(() => new CustomAnimationEffect(animation, speed: 0));
    }

    private static RgbColor[] Render(IEffect effect, TimeSpan elapsed, LedLayout layout)
    {
        var buffer = new RgbColor[layout.Count];
        effect.Render(new EffectFrame(elapsed, layout, EffectContext.Empty), buffer);
        return buffer;
    }

    private static CustomAnimation Parse(string json)
    {
        var result = CustomAnimationParser.Parse(json);
        Assert.True(result.Succeeded, result.Error);
        return result.Animation!;
    }
}
