using System.IO;
using System.Xml.Linq;
using Porchlight.App.Controls;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Shell;

public sealed class ThemePaletteTests
{
    [Theory]
    [InlineData(AppTheme.Dark, null, true)]
    [InlineData(AppTheme.Dark, true, true)]
    [InlineData(AppTheme.Light, false, false)]
    [InlineData(AppTheme.Light, null, false)]
    [InlineData(AppTheme.System, false, true)]
    [InlineData(AppTheme.System, true, false)]
    [InlineData(AppTheme.System, null, false)]
    public void IsDark_FollowsTheChoiceAndWindowsForSystem(AppTheme theme, bool? appsUseLightTheme, bool expected) =>
        Assert.Equal(expected, ThemePalette.IsDark(theme, appsUseLightTheme));

    [Fact]
    public void LightAndDarkPalettes_DefineTheSameKeys()
    {
        Assert.Equal(PaletteKeys("Light").Order(), PaletteKeys("Dark").Order());
    }

    [Fact]
    public void EveryHue_HasAllFourBrushesInBothPalettes()
    {
        foreach (var variant in new[] { "Light", "Dark" })
        {
            var keys = PaletteKeys(variant).ToHashSet();
            foreach (var hue in Enum.GetValues<Hue>())
            {
                foreach (var part in new[] { "Brush", "Tint", "Stroke", "Wash" })
                {
                    Assert.Contains(HueBrushesKey(hue, part), keys);
                }
            }
        }
    }

    private static string HueBrushesKey(Hue hue, string part) => part == "Brush" ? $"Hue{hue}Brush" : $"Hue{hue}{part}Brush";

    private static IEnumerable<string> PaletteKeys(string variant)
    {
        var path = Path.Combine(RepoRoot(), "src", "Porchlight.App", "Themes", $"Palette.{variant}.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path).Root!.Elements().Select(e => (string)e.Attribute(x + "Key")!);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Porchlight.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
