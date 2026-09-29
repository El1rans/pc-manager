using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class CleanupPathsTests
{
    private static readonly string[] Protected =
    [
        @"C:\Windows",
        @"C:\Users\Test",
        @"C:\Program Files",
        @"C:\Users\Test\AppData\Local",
    ];

    [Theory]
    [InlineData(@"C:\Root", @"C:\Root\file.tmp", true)]
    [InlineData(@"C:\Root\", @"C:\Root\sub\file.tmp", true)]
    [InlineData(@"C:\ROOT", @"c:\root\File.TMP", true)]
    [InlineData(@"C:\Root", @"C:\Root", false)]
    [InlineData(@"C:\Root", @"C:\Root\", false)]
    [InlineData(@"C:\Root", @"C:\Root2\file.tmp", false)]
    [InlineData(@"C:\Root", @"C:\Root\..\Windows\evil.dll", false)]
    [InlineData(@"C:\Root", @"C:\Root\sub\..\..\Other\x", false)]
    [InlineData(@"C:\Root", @"D:\Root\file.tmp", false)]
    [InlineData(@"C:\Root", "", false)]
    [InlineData("", @"C:\Root\file.tmp", false)]
    public void IsInside_RequiresAStrictNormalisedPrefixWithSeparator(string root, string path, bool expected)
    {
        Assert.Equal(expected, CleanupPaths.IsInside(root, path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"relative\temp")]
    [InlineData("Temp")]
    [InlineData(@"C:\")]
    [InlineData(@"C:")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\")]
    [InlineData(@"c:\WINDOWS")]
    [InlineData(@"C:\Users\Test")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Users\Test\AppData\Local")]
    [InlineData(@"C:\Users")]
    public void IsDangerousRoot_RejectsEmptyRelativeDriveRootsAndProtectedFolders(string? root)
    {
        Assert.True(CleanupPaths.IsDangerousRoot(root, Protected));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp")]
    [InlineData(@"C:\Users\Test\AppData\Local\Temp")]
    [InlineData(@"C:\Users\Test\AppData\Local\CrashDumps")]
    public void IsDangerousRoot_AllowsAFolderInsideAProtectedOne(string root)
    {
        Assert.False(CleanupPaths.IsDangerousRoot(root, Protected));
    }
}
