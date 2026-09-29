using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>The "at least one copy per group always remains" rule, enforced in Core.</summary>
public sealed class DuplicateRemoverTests
{
    private const string Docs = @"C:\Fake\Docs";
    private const long Bytes = 2_000;

    private static readonly DateTime Base = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeRecycler _recycler = new();

    public DuplicateRemoverTests()
    {
        _fs.AddDirectory(Docs);
    }

    /// <summary>Puts the copies on the fake disk and returns the group; file i is i days newer than i-1.</summary>
    private DuplicateGroup MakeGroup(string name, int copies)
    {
        var files = new List<DuplicateFile>();
        for (var i = 0; i < copies; i++)
        {
            var path = Docs + $@"\{name}{i}.bin";
            var time = Base.AddDays(i);
            _fs.AddFile(path, Bytes, time);
            files.Add(new DuplicateFile(path, Path.GetFileName(path), Docs, time));
        }

        return new DuplicateGroup(Bytes, files.OrderByDescending(f => f.LastWriteUtc).ToList());
    }

    private Task<DuplicateRemoveResult> Remove(IReadOnlyList<DuplicateGroup> groups, IEnumerable<string> selected) =>
        RemoveWith(groups, selected, TestContext.Current.CancellationToken);

    private Task<DuplicateRemoveResult> RemoveWith(
        IReadOnlyList<DuplicateGroup> groups, IEnumerable<string> selected, CancellationToken token) =>
        new DuplicateRemover(_fs, _recycler, NullLogger<DuplicateRemover>.Instance)
            .RemoveAsync(groups, selected.ToList(), token);

    [Fact]
    public async Task Remove_MovesOnlyTheSelectedCopiesToTheRecycleBin()
    {
        var group = MakeGroup("a", 3);

        var result = await Remove([group], [group.Files[1].FullPath, group.Files[2].FullPath]);

        Assert.Equal(2, result.FilesRemoved);
        Assert.Equal(2 * Bytes, result.BytesFreed);
        Assert.Equal(0, result.GroupsKeptOne);
        Assert.DoesNotContain(group.Files[0].FullPath, _recycler.Recycled);
    }

    [Fact]
    public async Task Remove_SelectingEveryCopy_SparesTheNewest()
    {
        var group = MakeGroup("a", 3);

        var result = await Remove([group], group.Files.Select(f => f.FullPath));

        Assert.Equal(2, result.FilesRemoved);
        Assert.Equal(1, result.GroupsKeptOne);
        Assert.DoesNotContain(DuplicateSelection.PickNewest(group).FullPath, _recycler.Recycled);
    }

    [Fact]
    public async Task Remove_EveryPossibleSelection_AlwaysLeavesAtLeastOneCopy()
    {
        const int copies = 4;
        for (var mask = 0; mask < 1 << copies; mask++)
        {
            var fs = new FakeCleanupFileSystem();
            fs.AddDirectory(Docs);
            var recycler = new FakeRecycler();
            var files = new List<DuplicateFile>();
            for (var i = 0; i < copies; i++)
            {
                var path = Docs + $@"\m{i}.bin";
                var time = Base.AddDays(i);
                fs.AddFile(path, Bytes, time);
                files.Add(new DuplicateFile(path, $"m{i}.bin", Docs, time));
            }

            var group = new DuplicateGroup(Bytes, files);
            var selected = files.Where((_, i) => (mask & (1 << i)) != 0).Select(f => f.FullPath).ToList();

            await new DuplicateRemover(fs, recycler, NullLogger<DuplicateRemover>.Instance)
                .RemoveAsync([group], selected, TestContext.Current.CancellationToken);

            Assert.True(
                files.Any(f => !recycler.Recycled.Contains(f.FullPath)),
                $"Selection mask {mask} removed every copy.");
        }
    }

    [Fact]
    public async Task Remove_IgnoresPathsThatAreNotCopiesInAnyGroup()
    {
        var group = MakeGroup("a", 2);
        _fs.AddFile(Docs + @"\precious.doc", Bytes, Base);

        var result = await Remove([group], [Docs + @"\precious.doc", @"C:\Windows\System32\kernel32.dll"]);

        Assert.Equal(0, result.FilesRemoved);
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task Remove_WhenTheCopyToKeepIsGone_SkipsTheWholeGroup()
    {
        var group = MakeGroup("a", 2);
        var keep = group.Files[0];
        var remove = group.Files[1];
        _fs.DeleteFile(keep.FullPath); // vanished after the scan

        var result = await Remove([group], [remove.FullPath]);

        Assert.Equal(0, result.FilesRemoved);
        Assert.Equal(1, result.FilesChanged);
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task Remove_WhenTheCopyToKeepChanged_SkipsTheWholeGroup()
    {
        var group = MakeGroup("a", 2);
        _fs.AddFile(group.Files[0].FullPath, Bytes + 1, group.Files[0].LastWriteUtc); // edited since the scan

        var result = await Remove([group], [group.Files[1].FullPath]);

        Assert.Equal(0, result.FilesRemoved);
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task Remove_ACopyThatChangedSinceTheScan_IsLeftAlone()
    {
        var group = MakeGroup("a", 3);
        var edited = group.Files[1];
        _fs.AddFile(edited.FullPath, Bytes, edited.LastWriteUtc.AddMinutes(1));

        var result = await Remove([group], [edited.FullPath, group.Files[2].FullPath]);

        Assert.Equal(1, result.FilesRemoved);
        Assert.Equal(1, result.FilesChanged);
        Assert.Equal([group.Files[2].FullPath], _recycler.Recycled);
    }

    [Fact]
    public async Task Remove_ARefusedCopy_IsCountedAsFailed_AndNothingElseIsDone()
    {
        var group = MakeGroup("a", 3);
        _recycler.Refuse.Add(group.Files[1].FullPath);

        var result = await Remove([group], [group.Files[1].FullPath, group.Files[2].FullPath]);

        Assert.Equal(1, result.FilesFailed);
        Assert.Equal(1, result.FilesRemoved);
        Assert.Equal([group.Files[2].FullPath], _recycler.Recycled);
    }

    [Fact]
    public async Task Remove_StopsBetweenFilesWhenCancelled()
    {
        var group = MakeGroup("a", 4);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await RemoveWith([group], group.Files.Skip(1).Select(f => f.FullPath), cts.Token);

        Assert.True(result.WasCancelled);
        Assert.Empty(_recycler.Recycled);
    }

    [Fact]
    public async Task Remove_HandlesSeveralGroupsIndependently()
    {
        var first = MakeGroup("a", 2);
        var second = MakeGroup("b", 2);

        var result = await Remove([first, second], [first.Files[1].FullPath, .. second.Files.Select(f => f.FullPath)]);

        Assert.Equal(2, result.FilesRemoved);
        Assert.Equal(1, result.GroupsKeptOne);
        Assert.Equal(4_000, result.BytesFreed);
    }
}
