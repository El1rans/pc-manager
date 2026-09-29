using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class DuplicateSelectionTests
{
    private static readonly DateTime Base = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DuplicateFile File(string path, int days) =>
        new(path, Path.GetFileName(path), Path.GetDirectoryName(path)!, Base.AddDays(days));

    [Fact]
    public void SuggestKeepNewest_KeepsTheMostRecentlyModifiedCopy()
    {
        var group = new DuplicateGroup(10, [File(@"C:\a\one.bin", 1), File(@"C:\a\two.bin", 9), File(@"C:\a\three.bin", 5)]);

        var remove = DuplicateSelection.SuggestKeepNewest(group);

        Assert.Equal([@"C:\a\one.bin", @"C:\a\three.bin"], remove.Order());
        Assert.Equal(@"C:\a\two.bin", DuplicateSelection.PickNewest(group).FullPath);
    }

    [Fact]
    public void PickNewest_OnATie_PrefersTheShorterThenAlphabeticalPath()
    {
        var group = new DuplicateGroup(10, [File(@"C:\a\copy of x.bin", 3), File(@"C:\b\x.bin", 3), File(@"C:\a\x.bin", 3)]);

        Assert.Equal(@"C:\a\x.bin", DuplicateSelection.PickNewest(group).FullPath);
        Assert.Equal(2, DuplicateSelection.SuggestKeepNewest(group).Count);
    }
}
