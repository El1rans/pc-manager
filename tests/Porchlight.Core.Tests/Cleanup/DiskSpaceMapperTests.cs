using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class DiskSpaceMapperTests
{
    private const string Root = @"C:\Fake\Users\Test";
    private const string Videos = Root + @"\Videos";
    private static readonly DateTime Old = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeCleanupPathProvider _paths = new() { PersonalFolders = [Videos, Root + @"\Documents"] };

    private Task<DiskNode> Map(string root = Root) => MapWith(root, TestContext.Current.CancellationToken);

    private Task<DiskNode> MapWith(string root, CancellationToken token) =>
        new DiskSpaceMapper(_fs, _paths, NullLogger<DiskSpaceMapper>.Instance).MapAsync(root, null, token);

    [Fact]
    public async Task Map_AggregatesFileSizesUpTheTree_AndSortsChildrenLargestFirst()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\notes.txt", 10, Old)
            .AddDirectory(Videos)
            .AddFile(Videos + @"\a.mp4", 300, Old)
            .AddDirectory(Videos + @"\Old")
            .AddFile(Videos + @"\Old\b.mp4", 200, Old)
            .AddDirectory(Root + @"\Documents")
            .AddFile(Root + @"\Documents\c.doc", 50, Old);

        var root = await Map();

        Assert.Equal(560, root.TotalBytes);
        Assert.Equal(4, root.FileCount);
        Assert.Equal(["Videos", "Documents"], root.Children.Select(c => c.Name));
        var videos = root.Children[0];
        Assert.Equal(500, videos.TotalBytes);
        Assert.Equal(200, videos.Children.Single().TotalBytes);
        Assert.Equal(0, root.UnreadableFolders);
    }

    [Fact]
    public async Task Map_NeverFollowsOrCountsReparsePoints()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\real.bin", 100, Old)
            .AddReparseDirectory(Root + @"\Junction")
            .AddFile(Root + @"\Junction\huge.bin", 9_000_000, Old)
            .AddFile(Root + @"\link.bin", 5_000, Old, FileAttributes.ReparsePoint);

        var root = await Map();

        Assert.Equal(100, root.TotalBytes);
        Assert.Empty(root.Children);
        Assert.DoesNotContain(_fs.ListedDirectories, d => d.StartsWith(Root + @"\Junction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Map_SkipsUnreadableFolders_CountsThemAndStillTotalsTheRest()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\ok.bin", 100, Old)
            .AddDirectory(Root + @"\Locked")
            .AddFile(Root + @"\Locked\secret.bin", 999, Old)
            .MakeUnreadable(Root + @"\Locked")
            .AddDirectory(Root + @"\Fine")
            .AddDirectory(Root + @"\Fine\Nested")
            .MakeUnreadable(Root + @"\Fine\Nested");

        var root = await Map();

        Assert.Equal(100, root.TotalBytes);
        Assert.Equal(2, root.UnreadableFolders);
        Assert.Equal(1, root.Children.Single(c => c.Name == "Fine").UnreadableFolders);
    }

    [Fact]
    public async Task Map_CloudOnlyPlaceholdersCountAsZeroBytes()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\cloud.bin", 5_000, Old, FileAttributes.Offline)
            .AddFile(Root + @"\recall.bin", 5_000, Old, (FileAttributes)0x00400000)
            .AddFile(Root + @"\local.bin", 70, Old);

        var root = await Map();

        Assert.Equal(70, root.TotalBytes);
        Assert.Equal(["local.bin"], root.Files.Select(f => f.Name));
    }

    [Fact]
    public async Task Map_KeepsOnlyTheLargestFilesPerFolder_ButTotalsEverything()
    {
        _fs.AddDirectory(Root);
        long total = 0;
        for (var i = 1; i <= DiskSpaceMapper.MaxFilesPerFolder + 5; i++)
        {
            _fs.AddFile(Root + $@"\f{i}.bin", i, Old);
            total += i;
        }

        var root = await Map();

        Assert.Equal(total, root.TotalBytes);
        Assert.Equal(DiskSpaceMapper.MaxFilesPerFolder, root.Files.Count);
        Assert.Equal(DiskSpaceMapper.MaxFilesPerFolder + 5, root.Files[0].Bytes);
    }

    [Fact]
    public async Task Map_OnlyOffersRecycleForOrdinaryFilesInPersonalFolders()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\outside.bin", 10, Old)
            .AddDirectory(Videos)
            .AddFile(Videos + @"\movie.mp4", 20, Old)
            .AddFile(Videos + @"\hidden.mp4", 30, Old, FileAttributes.Hidden)
            .AddFile(Videos + @"\sys.mp4", 40, Old, FileAttributes.System);

        var root = await Map();

        Assert.False(root.Files.Single().CanRecycle);
        var videos = root.Children.Single();
        Assert.True(videos.Files.Single(f => f.Name == "movie.mp4").CanRecycle);
        Assert.False(videos.Files.Single(f => f.Name == "hidden.mp4").CanRecycle);
        Assert.False(videos.Files.Single(f => f.Name == "sys.mp4").CanRecycle);
        Assert.Equal(90, videos.TotalBytes);
    }

    [Fact]
    public async Task Map_StopsWhenCancelled()
    {
        _fs.AddDirectory(Root).AddDirectory(Root + @"\A").AddDirectory(Root + @"\B");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MapWith(Root, cts.Token));
        Assert.Empty(_fs.ListedDirectories);
    }

    [Fact]
    public async Task Map_MissingRoot_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Map(@"C:\Fake\Nope"));
    }

    [Fact]
    public async Task RemoveFile_ReducesTotalsUpTheChain()
    {
        _fs.AddDirectory(Root)
            .AddDirectory(Videos)
            .AddFile(Videos + @"\movie.mp4", 300, Old)
            .AddFile(Videos + @"\keep.mp4", 100, Old);
        var root = await Map();
        var videos = root.Children.Single();
        var movie = videos.Files.Single(f => f.Name == "movie.mp4");

        Assert.True(videos.RemoveFile(movie));

        Assert.Equal(100, videos.TotalBytes);
        Assert.Equal(100, root.TotalBytes);
        Assert.Equal(1, root.FileCount);
        Assert.False(videos.RemoveFile(movie));
    }
}
