using PCManager.Core.Winget;
using Xunit;

namespace PCManager.Core.Tests.Winget;

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

    [Fact]
    public void Parse_EmptyOutput_ReturnsNoPackages()
    {
        var packages = WingetTableParser.Parse([]);

        Assert.Empty(packages);
    }

    [Fact]
    public void Parse_NoInstalledPackageFound_ReturnsNoPackages()
    {
        string[] lines = ["No installed package found matching input criteria."];

        var packages = WingetTableParser.Parse(lines);

        Assert.Empty(packages);
    }

}
