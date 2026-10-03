using Porchlight.Core.RemoveApps;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.RemoveApps;

public sealed class WingetIdMapperTests
{
    private static WingetInstalledPackage Pkg(string name, string id, string version = "1.0") => new(name, id, version);

    [Fact]
    public void FindId_SingleNameMatch_ReturnsTheId()
    {
        var id = WingetIdMapper.FindId(AppProtectionRulesTests.App("VLC media player"), [Pkg("VLC media player", "VideoLAN.VLC")]);

        Assert.Equal("VideoLAN.VLC", id);
    }

    [Fact]
    public void FindId_IsCaseInsensitive()
    {
        var id = WingetIdMapper.FindId(AppProtectionRulesTests.App("vlc MEDIA player"), [Pkg("VLC media player", "VideoLAN.VLC")]);

        Assert.Equal("VideoLAN.VLC", id);
    }

    [Fact]
    public void FindId_NoMatch_IsNull() =>
        Assert.Null(WingetIdMapper.FindId(AppProtectionRulesTests.App("Unknown"), [Pkg("Other", "Foo.Other")]));

    [Fact]
    public void FindId_PartialNameIsNotAMatch() =>
        Assert.Null(WingetIdMapper.FindId(AppProtectionRulesTests.App("Zoom Workplace"), [Pkg("Zoom", "Zoom.Zoom")]));

    [Fact]
    public void FindId_TruncatedWingetName_MatchesAsPrefix()
    {
        var app = AppProtectionRulesTests.App("Microsoft Visual Studio Code (User)");

        Assert.Equal("Microsoft.VisualStudioCode", WingetIdMapper.FindId(app, [Pkg("Microsoft Visual Studio Co…", "Microsoft.VisualStudioCode")]));
        Assert.Equal("Microsoft.VisualStudioCode", WingetIdMapper.FindId(app, [Pkg("Microsoft Visual Studio...", "Microsoft.VisualStudioCode")]));
    }

    [Fact]
    public void FindId_SeveralWithSameName_UsesTheVersionToChoose()
    {
        var app = AppProtectionRulesTests.App("Python", version: "3.12.1");
        var packages = new[] { Pkg("Python", "Python.Python.3.11", "3.11.5"), Pkg("Python", "Python.Python.3.12", "3.12.1") };

        Assert.Equal("Python.Python.3.12", WingetIdMapper.FindId(app, packages));
    }

    [Fact]
    public void FindId_AmbiguousEvenWithVersion_IsNull()
    {
        var app = AppProtectionRulesTests.App("Python", version: "3.12.1");
        var packages = new[] { Pkg("Python", "A.Python", "3.12.1"), Pkg("Python", "B.Python", "3.12.1") };

        Assert.Null(WingetIdMapper.FindId(app, packages));
    }

    [Fact]
    public void FindId_SeveralWithSameNameAndNoVersion_IsNull()
    {
        var app = AppProtectionRulesTests.App("Python", version: null);
        var packages = new[] { Pkg("Python", "A.Python"), Pkg("Python", "B.Python") };

        Assert.Null(WingetIdMapper.FindId(app, packages));
    }

    [Fact]
    public void ListParser_ReadsNameIdAndVersion()
    {
        string[] output =
        [
            "Name                    Id                          Version      Available",
            "------------------------------------------------------------------------------",
            "Adobe Acrobat (64-bit)  Adobe.Acrobat.Reader.64-bit 26.002.21931 ",
            "Battle.net              Blizzard.BattleNet          Unknown      1.19.3.3219",
        ];

        var packages = WingetInstalledListParser.Parse(output);

        Assert.Equal(
            [new WingetInstalledPackage("Adobe Acrobat (64-bit)", "Adobe.Acrobat.Reader.64-bit", "26.002.21931"),
             new WingetInstalledPackage("Battle.net", "Blizzard.BattleNet", "Unknown")],
            packages);
    }

    [Fact]
    public void ListParser_NoTable_IsEmpty() =>
        Assert.Empty(WingetInstalledListParser.Parse(["No installed package found matching input criteria."]));
}
