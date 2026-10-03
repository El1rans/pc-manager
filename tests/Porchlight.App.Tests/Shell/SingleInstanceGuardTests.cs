using Porchlight.App.Shell;
using Xunit;

namespace Porchlight.App.Tests.Shell;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void NormalRun_UsesTheFixedReleaseNames()
    {
        var names = SingleInstanceGuard.GetObjectNames(null);

        Assert.Equal(@"Local\PorchlightSingleInstance", names.Mutex);
        Assert.Equal(@"Local\PorchlightActivate", names.ActivateEvent);
    }

    [Fact]
    public void OverrideRun_UsesDevNamesWithAShortHash()
    {
        var names = SingleInstanceGuard.GetObjectNames(@"C:\Temp\porchlight-check");

        Assert.Matches(@"^Local\\PorchlightSingleInstance-dev-[0-9A-F]{12}$", names.Mutex);
        Assert.Matches(@"^Local\\PorchlightActivate-dev-[0-9A-F]{12}$", names.ActivateEvent);
        Assert.Equal(names.Mutex[^12..], names.ActivateEvent[^12..]);
    }

    [Theory]
    [InlineData(@"c:\temp\porchlight-check")]
    [InlineData(@"C:\TEMP\Porchlight-Check")]
    [InlineData(@"C:\Temp\porchlight-check\")]
    public void SameFolder_GetsTheSameNames(string root)
    {
        Assert.Equal(
            SingleInstanceGuard.GetObjectNames(@"C:\Temp\porchlight-check"),
            SingleInstanceGuard.GetObjectNames(root));
    }

    [Fact]
    public void DifferentFolders_GetDifferentNames()
    {
        var a = SingleInstanceGuard.GetObjectNames(@"C:\Temp\check-a");
        var b = SingleInstanceGuard.GetObjectNames(@"C:\Temp\check-b");

        Assert.NotEqual(a.Mutex, b.Mutex);
        Assert.NotEqual(a.ActivateEvent, b.ActivateEvent);
    }
}
