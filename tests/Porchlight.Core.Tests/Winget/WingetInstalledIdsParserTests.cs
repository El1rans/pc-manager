using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetInstalledIdsParserTests
{
    // Modelled on a real "winget list --source winget" run (a few rows).
    private static readonly string[] RealListOutput =
    [
        "Name                    Id                          Version      Available",
        "------------------------------------------------------------------------------",
        "Adobe Acrobat (64-bit)  Adobe.Acrobat.Reader.64-bit 26.002.21931 ",
        "AnyDesk                 AnyDesk.AnyDesk             ad 9.8.0     ",
        "Battle.net              Blizzard.BattleNet          Unknown      1.19.3.3219",
    ];

    [Fact]
    public void Parse_RealTable_ReturnsAllIds()
    {
        var ids = WingetInstalledIdsParser.Parse(RealListOutput);

        Assert.Equal(3, ids.Count);
        Assert.Contains("AnyDesk.AnyDesk", ids);
        Assert.Contains("Blizzard.BattleNet", ids);
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        var ids = WingetInstalledIdsParser.Parse(RealListOutput);

        Assert.Contains("adobe.acrobat.reader.64-bit", ids);
    }

    [Fact]
    public void Parse_NoTable_ReturnsEmpty() =>
        Assert.Empty(WingetInstalledIdsParser.Parse(["No installed package found matching input criteria."]));
}
