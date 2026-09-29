using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects.CustomAnimations;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects.CustomAnimations;

public sealed class CustomAnimationParserTests
{
    private const string Minimal = """
        { "name": "Red", "frames": [ { "fill": "#FF0000" } ] }
        """;

    [Fact]
    public void Parse_Minimal_AppliesDefaults()
    {
        var animation = ParseOk(Minimal);

        Assert.Equal("Red", animation.Name);
        Assert.True(animation.Loop);
        Assert.Equal(CustomAnimationTransition.Cut, animation.Transition);
        var frame = Assert.Single(animation.Frames);
        Assert.Equal(TimeSpan.FromSeconds(CustomAnimationParser.DefaultFrameSeconds), frame.Duration);
        Assert.Equal(new RgbColor(255, 0, 0), frame.Sample(0.5, 0.5));
    }

    [Fact]
    public void Parse_FullDocument_ReadsEveryField()
    {
        var animation = ParseOk("""
            {
              "format": "porchlight-animation",
              "version": 1,
              "name": "  Demo  ",
              "description": "A demo",
              "author": "Me",
              "loop": false,
              "transition": "fade",
              "frameDuration": 0.5,
              "palette": { ".": "#000000", "G": "#00FF00" },
              "frames": [
                { "rows": ["G.", ".G"] },
                { "gradient": ["#FF0000", "G"], "duration": 2 },
                { "fill": "." }
              ]
            }
            """);

        Assert.Equal("Demo", animation.Name);
        Assert.Equal("A demo", animation.Description);
        Assert.Equal("Me", animation.Author);
        Assert.False(animation.Loop);
        Assert.Equal(CustomAnimationTransition.Fade, animation.Transition);
        Assert.Equal(3, animation.Frames.Count);
        Assert.Equal(TimeSpan.FromSeconds(0.5), animation.Frames[0].Duration);
        Assert.Equal(TimeSpan.FromSeconds(2), animation.Frames[1].Duration);
        Assert.Equal(TimeSpan.FromSeconds(3), animation.TotalDuration);

        var rows = animation.Frames[0];
        Assert.Equal((2, 2), (rows.Width, rows.Height));
        Assert.Equal(new RgbColor(0, 255, 0), rows[0, 0]);
        Assert.Equal(RgbColor.Black, rows[1, 0]);
        Assert.Equal(new RgbColor(0, 255, 0), rows[1, 1]);

        Assert.True(animation.Frames[1].IsGradient);
        Assert.Equal(new RgbColor(0, 255, 0), animation.Frames[1][1, 0]);
    }

    [Fact]
    public void Parse_AcceptsCommentsTrailingCommasAndMarkdownFence()
    {
        var animation = ParseOk("""
            ```json
            {
              // a comment the AI left in
              "name": "Fenced",
              "frames": [ { "fill": "#0000FF" }, ],
            }
            ```
            """);

        Assert.Equal("Fenced", animation.Name);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("not json", "valid JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("""{ "frames": [ { "fill": "#FF0000" } ] }""", "\"name\" is required")]
    [InlineData("""{ "name": "x" }""", "\"frames\" is required")]
    [InlineData("""{ "name": "x", "frames": [] }""", "between 1 and")]
    [InlineData("""{ "format": "other", "name": "x", "frames": [ { "fill": "#FF0000" } ] }""", "\"format\"")]
    [InlineData("""{ "version": 2, "name": "x", "frames": [ { "fill": "#FF0000" } ] }""", "\"version\"")]
    [InlineData("""{ "name": "x", "transition": "wipe", "frames": [ { "fill": "#FF0000" } ] }""", "\"transition\"")]
    [InlineData("""{ "name": "x", "loop": "yes", "frames": [ { "fill": "#FF0000" } ] }""", "\"loop\"")]
    [InlineData("""{ "name": "x", "frameDuration": 0, "frames": [ { "fill": "#FF0000" } ] }""", "\"frameDuration\"")]
    [InlineData("""{ "name": "x", "frames": [ { "fill": "#FF0000", "duration": 9999 } ] }""", "Frame 1: \"duration\"")]
    [InlineData("""{ "name": "x", "frames": [ { "fill": "red" } ] }""", "Frame 1: \"fill\"")]
    [InlineData("""{ "name": "x", "frames": [ { "fill": "#FFF" } ] }""", "#RRGGBB")]
    [InlineData("""{ "name": "x", "frames": [ { } ] }""", "exactly one of")]
    [InlineData("""{ "name": "x", "frames": [ { "fill": "#FF0000", "gradient": ["#000000", "#FFFFFF"] } ] }""", "exactly one of")]
    [InlineData("""{ "name": "x", "frames": [ { "gradient": ["#000000"] } ] }""", "2 to 64 colors")]
    [InlineData("""{ "name": "x", "frames": [ { "rows": ["ab"] } ] }""", "needs a \"palette\"")]
    [InlineData("""{ "name": "x", "palette": { "ab": "#000000" }, "frames": [ { "fill": "#FF0000" } ] }""", "exactly one character")]
    [InlineData("""{ "name": "x", "palette": { "a": "blue" }, "frames": [ { "fill": "#FF0000" } ] }""", "Palette entry \"a\"")]
    public void Parse_Invalid_ReturnsReadableError(string json, string expectedErrorFragment)
    {
        var result = CustomAnimationParser.Parse(json);

        Assert.False(result.Succeeded);
        Assert.Null(result.Animation);
        Assert.Contains(expectedErrorFragment, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RowCharacterMissingFromPalette_NamesFrameRowAndCharacter()
    {
        var result = CustomAnimationParser.Parse("""
            { "name": "x", "palette": { ".": "#000000" }, "frames": [ { "fill": "." }, { "rows": ["..", ".X"] } ] }
            """);

        Assert.Equal("Frame 2, row 2: 'X' is not in the palette.", result.Error);
    }

    [Fact]
    public void Parse_RaggedRows_Rejected()
    {
        var result = CustomAnimationParser.Parse("""
            { "name": "x", "palette": { ".": "#000000" }, "frames": [ { "rows": ["...", ".."] } ] }
            """);

        Assert.Contains("every row must be the same length", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OverSizeLimits_Rejected()
    {
        var tooWide = new string('.', CustomAnimationParser.MaxGridSize + 1);
        var wide = CustomAnimationParser.Parse(
            $$"""{ "name": "x", "palette": { ".": "#000000" }, "frames": [ { "rows": ["{{tooWide}}"] } ] }""");
        Assert.False(wide.Succeeded);

        var manyFrames = string.Join(",", Enumerable.Repeat("""{ "fill": "#000000" }""", CustomAnimationParser.MaxFrames + 1));
        var many = CustomAnimationParser.Parse($$"""{ "name": "x", "frames": [ {{manyFrames}} ] }""");
        Assert.False(many.Succeeded);

        var huge = CustomAnimationParser.Parse(new string(' ', CustomAnimationParser.MaxDocumentLength + 1));
        Assert.False(huge.Succeeded);
    }

    [Fact]
    public void Parse_NameWithControlCharacters_StripsThem()
    {
        var animation = ParseOk("""{ "name": "Two\nLines\t!", "frames": [ { "fill": "#000000" } ] }""");

        Assert.Equal("TwoLines!", animation.Name);
    }

    [Fact]
    public void Parse_NameTooLong_Rejected()
    {
        var name = new string('a', CustomAnimationParser.MaxNameLength + 1);

        var result = CustomAnimationParser.Parse($$"""{ "name": "{{name}}", "frames": [ { "fill": "#000000" } ] }""");

        Assert.Contains("too long", result.Error, StringComparison.Ordinal);
    }

    private static CustomAnimation ParseOk(string json)
    {
        var result = CustomAnimationParser.Parse(json);
        Assert.True(result.Succeeded, result.Error);
        return result.Animation!;
    }
}
