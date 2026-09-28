using Porchlight.Core.Components;
using Xunit;

namespace Porchlight.Core.Tests.Components;

public sealed class ComponentCatalogTests
{
    [Theory]
    [InlineData(ComponentIds.AnyDesk, "AnyDesk.AnyDesk")]
    [InlineData(ComponentIds.OpenRgb, "OpenRGB.OpenRGB")]
    [InlineData(ComponentIds.PawnIo, "namazso.PawnIO")]
    public void Get_KnownId_HasExpectedWingetId(string id, string expectedWingetId)
    {
        var definition = ComponentCatalog.Get(id);

        Assert.Equal(expectedWingetId, definition.WingetId);
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentCatalog.Get("not-a-real-id"));
    }

    [Fact]
    public void All_HasExactlyTheThreeMilestoneComponents()
    {
        Assert.Equal(3, ComponentCatalog.All.Count);
        Assert.Equal(3, ComponentCatalog.All.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void PawnIo_RequiresServiceCheck_AndHasNoStartNotion()
    {
        var pawnIo = ComponentCatalog.Get(ComponentIds.PawnIo);

        Assert.Equal("PawnIO", pawnIo.ServiceName);
        Assert.Null(pawnIo.StartArguments);
        Assert.True(pawnIo.RequiresAdmin);
    }

    [Fact]
    public void OpenRgb_StartsWithServerAndMinimized()
    {
        var openRgb = ComponentCatalog.Get(ComponentIds.OpenRgb);

        Assert.Equal(["--server", "--startminimized"], openRgb.StartArguments);
    }
}
