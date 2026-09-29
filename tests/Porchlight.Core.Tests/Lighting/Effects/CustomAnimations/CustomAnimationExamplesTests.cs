using Porchlight.Core.Lighting.Effects.CustomAnimations;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects.CustomAnimations;

/// <summary>Keeps the AI prompt template, docs/custom-animations.md and the example files in
/// docs/animations honest: every example they show must actually import, and the guide must show
/// the exact prompt "Copy AI prompt" copies.</summary>
public sealed class CustomAnimationExamplesTests
{
    [Fact]
    public void PromptTemplate_ExampleParses()
    {
        var template = CustomAnimationPrompt.Template;
        var start = template.IndexOf("EXAMPLE", StringComparison.Ordinal);
        var example = template[template.IndexOf('{', start)..];

        var result = CustomAnimationParser.Parse(example);

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void PromptTemplate_FormatSketchParses()
    {
        // The FORMAT block (with its // comments and "frames": [ ... ]) is not meant to be valid on
        // its own, but with a real frame list it must be - so the field names it teaches are right.
        var template = CustomAnimationPrompt.Template;
        var start = template.IndexOf("FORMAT", StringComparison.Ordinal);
        var end = template.IndexOf("Each frame has", StringComparison.Ordinal);
        var sketch = template[template.IndexOf('{', start)..end]
            .Replace("[ ... ]", """[ { "fill": "R" } ]""", StringComparison.Ordinal);

        var result = CustomAnimationParser.Parse(sketch);

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void PromptTemplate_ContainsPlaceholders()
    {
        Assert.Contains(CustomAnimationPrompt.IdeaPlaceholder, CustomAnimationPrompt.Template, StringComparison.Ordinal);
        Assert.Contains(CustomAnimationPrompt.DevicePlaceholder, CustomAnimationPrompt.Template, StringComparison.Ordinal);
    }

    public static TheoryData<string> ExampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(ExamplesFolder(), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void DocsExample_Parses(string fileName)
    {
        var result = CustomAnimationParser.Parse(File.ReadAllText(Path.Combine(ExamplesFolder(), fileName)));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(Path.GetFileNameWithoutExtension(fileName), CustomAnimationLibrary.IdFor(result.Animation!.Name));
    }

    [Fact]
    public void UserGuide_JsonExamplesParse_AndPromptMatchesTemplate()
    {
        var guide = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "custom-animations.md")).ReplaceLineEndings("\n");

        var jsonBlocks = guide.Split("```json\n").Skip(1).Select(b => b[..b.IndexOf("```", StringComparison.Ordinal)]).ToList();
        Assert.NotEmpty(jsonBlocks);
        Assert.All(jsonBlocks, block => Assert.True(CustomAnimationParser.Parse(block).Succeeded, block));

        Assert.Contains(CustomAnimationPrompt.Template.ReplaceLineEndings("\n"), guide, StringComparison.Ordinal);
    }

    private static string ExamplesFolder() => Path.Combine(RepoRoot(), "docs", "animations");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Porchlight.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
