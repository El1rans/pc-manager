using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetSearchTableParserTests
{
    // Captured from a real "winget search --query vlc --source winget" run.
    private static readonly string[] RealVlcOutput =
    [
        "Name                       Id                         Version                 Match",
        "------------------------------------------------------------------------------------------",
        "VLC media player           VideoLAN.VLC               3.0.23                  Moniker: vlc",
        "pympress                   Cimbali.pympress           1.8.6                   Tag: vlc",
        "Jellyfin VLC Bridge        CrySer66.JellyfinVlcBridge 1.18.0                  Tag: vlc",
        "VLC media player (Nightly) VideoLAN.VLC.Nightly       4.0.0.0-nightly20260509 ",
    ];

    [Fact]
    public void Parse_RealTable_ReturnsNameIdVersion()
    {
        var results = WingetSearchTableParser.Parse(RealVlcOutput);

        Assert.Equal(4, results.Count);
        Assert.Equal(new WingetSearchResult("VLC media player", "VideoLAN.VLC", "3.0.23"), results[0]);
        Assert.Equal(new WingetSearchResult("Jellyfin VLC Bridge", "CrySer66.JellyfinVlcBridge", "1.18.0"), results[2]);
    }

    [Fact]
    public void Parse_RowWithEmptyMatchColumn_StillParsed()
    {
        var results = WingetSearchTableParser.Parse(RealVlcOutput);

        Assert.Equal(
            new WingetSearchResult("VLC media player (Nightly)", "VideoLAN.VLC.Nightly", "4.0.0.0-nightly20260509"),
            results[3]);
    }

    [Fact]
    public void Parse_TableWithoutMatchColumn_Parses()
    {
        string[] lines =
        [
            "Name                            Id                                 Version",
            "----------------------------------------------------------------------------------",
            "WhatsappTray                    D4koon.WhatsappTray                1.9.0.0",
            "WhatsApp AI Pro                 FernloopTechnologies.WhatsAppAIPro 2.0.14",
        ];

        var results = WingetSearchTableParser.Parse(lines);

        Assert.Equal(2, results.Count);
        Assert.Equal("FernloopTechnologies.WhatsAppAIPro", results[1].Id);
        Assert.Equal("2.0.14", results[1].Version);
    }

    [Fact]
    public void Parse_TruncatedName_KeepsEllipsisInName()
    {
        string[] lines =
        [
            "Name                        Id             Version Match",
            "----------------------------------------------------------------------",
            "A very long application na… Vendor.LongApp 1.0.0   Tag: long",
        ];

        var results = WingetSearchTableParser.Parse(lines);

        Assert.Single(results);
        Assert.Equal("A very long application na…", results[0].Name);
        Assert.Equal("Vendor.LongApp", results[0].Id);
    }

    [Fact]
    public void Parse_TruncatedId_IsDropped()
    {
        string[] lines =
        [
            "Name       Id                       Version Match",
            "----------------------------------------------------------------------",
            "Good       Vendor.Good              1.0.0   Tag: x",
            "Bad        Vendor.Some.Very.Long.I… 2.0.0   Tag: x",
        ];

        var results = WingetSearchTableParser.Parse(lines);

        Assert.Equal(["Vendor.Good"], results.Select(r => r.Id));
    }

    [Fact]
    public void Parse_WideCjkName_KeepsColumnsAligned()
    {
        // Each CJK ideograph takes two display cells, so winget pads these rows with fewer spaces.
        string[] lines =
        [
            "Name                 Id              Version Match",
            "----------------------------------------------------------",
            "日本語アプリ         Vendor.Nihongo  2.1     Tag: jp",
            "Plain app            Vendor.Plain    3.0     Tag: jp",
        ];

        var results = WingetSearchTableParser.Parse(lines);

        Assert.Equal(2, results.Count);
        Assert.Equal("日本語アプリ", results[0].Name);
        Assert.Equal("Vendor.Nihongo", results[0].Id);
        Assert.Equal("2.1", results[0].Version);
    }

    [Fact]
    public void Parse_NoPackageFound_ReturnsEmpty()
    {
        var results = WingetSearchTableParser.Parse(["No package found matching input criteria."]);

        Assert.Empty(results);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty() =>
        Assert.Empty(WingetSearchTableParser.Parse([]));
}
