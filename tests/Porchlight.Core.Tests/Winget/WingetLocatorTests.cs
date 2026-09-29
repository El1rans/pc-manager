using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetLocatorTests
{
    private const string LocalAppData = @"C:\Users\u\AppData\Local";
    private const string Alias = @"C:\Users\u\AppData\Local\Microsoft\WindowsApps\winget.exe";

    [Fact]
    public void Resolve_PrefersAppExecutionAlias()
    {
        var result = WingetLocator.Resolve(LocalAppData, @"C:\tools", _ => true);

        Assert.Equal(Alias, result);
    }

    [Fact]
    public void Resolve_NoAlias_SearchesRootedPathEntriesOnly()
    {
        var result = WingetLocator.Resolve(
            LocalAppData, @".;relative\dir;C:\empty;C:\tools", p => p == @"C:\tools\winget.exe");

        Assert.Equal(@"C:\tools\winget.exe", result);
    }

    [Fact]
    public void Resolve_RelativePathEntryIsNeverConsulted()
    {
        var probed = new List<string>();

        var result = WingetLocator.Resolve(LocalAppData, @".;relative", p => { probed.Add(p); return true; });

        Assert.Equal(Alias, probed.Single());
        Assert.Equal(Alias, result);
    }

    [Fact]
    public void Resolve_NothingFound_FallsBackToBareName()
    {
        Assert.Equal("winget", WingetLocator.Resolve(LocalAppData, @"C:\tools", _ => false));
        Assert.Equal("winget", WingetLocator.Resolve(null, null, _ => false));
    }
}
