using Porchlight.Core.Backup;
using Xunit;

namespace Porchlight.Core.Tests.Backup;

public sealed class KnownFolderCoverageTests
{
    private static readonly string[] Roots = [@"C:\Users\Zelda\OneDrive"];

    [Theory]
    [InlineData(@"C:\Users\Zelda\OneDrive\Desktop")]
    [InlineData(@"c:\users\zelda\onedrive\Documents\")]
    public void InsideOneDrive_IsProtected(string folder) => Assert.True(KnownFolderCoverage.IsInside(folder, Roots));

    [Theory]
    [InlineData(@"C:\Users\Zelda\Desktop")]
    [InlineData(@"C:\Users\Zelda\OneDrive - Contoso\Desktop")]
    [InlineData(@"C:\Users\Zelda\OneDriveOld\Desktop")]
    [InlineData(@"C:\Users\Zelda\OneDrive")]
    [InlineData("")]
    [InlineData(null)]
    public void OutsideOneDrive_IsNot(string? folder) => Assert.False(KnownFolderCoverage.IsInside(folder, Roots));

    [Fact]
    public void AnyOfSeveralRoots_Counts() =>
        Assert.True(KnownFolderCoverage.IsInside(
            @"C:\Users\Zelda\OneDrive - Contoso\Documents", [@"C:\Users\Zelda\OneDrive", @"C:\Users\Zelda\OneDrive - Contoso"]));

    [Fact]
    public void EnvironmentVariables_AreExpanded()
    {
        var profile = Environment.GetEnvironmentVariable("USERPROFILE");
        Assert.NotNull(profile);

        Assert.True(KnownFolderCoverage.IsInside(@"%USERPROFILE%\OneDrive\Pictures", [Path.Combine(profile, "OneDrive")]));
    }

    [Fact]
    public void MalformedPath_IsNotProtected() =>
        Assert.False(KnownFolderCoverage.IsInside("C:\\bad|path\0", Roots));

    [Fact]
    public void NoRoots_IsNotProtected() => Assert.False(KnownFolderCoverage.IsInside(@"C:\Users\Zelda\OneDrive\Desktop", []));
}
