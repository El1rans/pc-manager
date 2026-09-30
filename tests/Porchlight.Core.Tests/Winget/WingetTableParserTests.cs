using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetTableParserTests
{
    // The real sample from docs/specs/02-updates.md, produced by `winget upgrade`.
    private static readonly string[] SampleLines =
    [
        "Name                                Id                         Version       Available     Source",
        "-------------------------------------------------------------------------------------------------",
        "Battle.net                          Blizzard.BattleNet         Unknown       1.19.3.3219   winget",
        "GOG GALAXY                          GOG.Galaxy                 Unknown       2.1.8.30      winget",
        "Google Cloud SDK                    Google.CloudSDK            Unknown       586.0.0       winget",
        "Microsoft Edge                      Microsoft.Edge             153.0.4234.48 154.0.4258.37 winget",
        "Microsoft GameInput                 Microsoft.GameInput        3.5.274.0     3.5.278       winget",
        "Microsoft Visual Studio Code (User) Microsoft.VisualStudioCode 1.139.0       1.139.1       winget",
        "OBS Studio                          OBSProject.OBSStudio       31.0.3        32.2.2        winget",
        "Oh My Posh version 24.8.0           JanDeDobbeleer.OhMyPosh    24.8.0        31.3.0        winget",
        "WinRAR 6.24 (64-bit)                RARLab.WinRAR              6.24.0        7.23.0        winget",
        "10 upgrades available.",
        "",
        "The following packages have an upgrade available, but require explicit targeting for upgrade:",
        "Name    Id              Version  Available Source",
        "-------------------------------------------------",
        "Discord Discord.Discord 1.0.9258 1.0.9259  winget",
    ];

    [Fact]
    public void Parse_RealSample_Returns10Packages()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        Assert.Equal(10, packages.Count);
    }

    [Fact]
    public void Parse_RealSample_SecondTablePackageRequiresExplicit()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        var discord = Assert.Single(packages, p => p.Id == "Discord.Discord");
        Assert.True(discord.RequiresExplicit);
        Assert.Equal("Discord", discord.Name);
        Assert.Equal("1.0.9258", discord.InstalledVersion);
        Assert.Equal("1.0.9259", discord.AvailableVersion);
        Assert.Equal("winget", discord.Source);
    }

    [Fact]
    public void Parse_RealSample_FirstTablePackagesDoNotRequireExplicit()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        Assert.All(packages.Where(p => p.Id != "Discord.Discord"), p => Assert.False(p.RequiresExplicit));
    }

    [Fact]
    public void Parse_RealSample_NameWithSpacesKeptIntact()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        var vsCode = Assert.Single(packages, p => p.Id == "Microsoft.VisualStudioCode");
        Assert.Equal("Microsoft Visual Studio Code (User)", vsCode.Name);
    }

    [Fact]
    public void Parse_RealSample_UnknownVersionKept()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        var battleNet = Assert.Single(packages, p => p.Id == "Blizzard.BattleNet");
        Assert.Equal("Unknown", battleNet.InstalledVersion);
    }

    [Fact]
    public void Parse_RealSample_SummaryLineAndBlankLineIgnored()
    {
        var packages = WingetTableParser.Parse(SampleLines);

        Assert.DoesNotContain(packages, p => p.Name.Contains("upgrades available", StringComparison.Ordinal));
        Assert.DoesNotContain(packages, p => string.IsNullOrWhiteSpace(p.Name));
    }

    public static TheoryData<string[]> OutputsWithoutATable
    {
        get
        {
            var data = new TheoryData<string[]>();
            data.Add([]);
            data.Add(["No installed package found matching input criteria."]);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(OutputsWithoutATable))]
    public void Parse_EmptyOutputOrNoInstalledPackageFound_ReturnsNoPackages(string[] lines)
    {
        var packages = WingetTableParser.Parse(lines);

        Assert.Empty(packages);
    }

    [Fact]
    public void Parse_NameWithWideCjkCharacters_ColumnsStayAligned()
    {
        // winget pads columns to a fixed DISPLAY-CELL width, not a fixed character count: each of
        // these 4 CJK characters is 1 string character but occupies 2 display cells, so this row's
        // raw string is shorter than the header despite lining up in the same columns. Column
        // widths chosen generously (20/10/10/10) so this stays readable; built with PadRight
        // (ASCII, so 1 cell per char) rather than hand-counted spaces.
        var header = "Name".PadRight(20) + "Id".PadRight(10) + "Version".PadRight(10) + "Available".PadRight(10) + "Source";
        var separator = new string('-', header.Length);
        // "微软商店" = 4 wide characters = 8 display cells, then 12 half-width spaces to fill the
        // rest of the 20-cell Name column (8 + 12 = 20) - 16 string characters total, not 20.
        var nameField = "微软商店" + new string(' ', 12);
        var row = nameField + "Ms.Store".PadRight(10) + "1.0".PadRight(10) + "2.0".PadRight(10) + "winget";
        string[] lines = [header, separator, row];

        var packages = WingetTableParser.Parse(lines);

        var package = Assert.Single(packages);
        Assert.Equal("微软商店", package.Name);
        Assert.Equal("Ms.Store", package.Id);
        Assert.Equal("1.0", package.InstalledVersion);
        Assert.Equal("2.0", package.AvailableVersion);
        Assert.Equal("winget", package.Source);
    }
}
