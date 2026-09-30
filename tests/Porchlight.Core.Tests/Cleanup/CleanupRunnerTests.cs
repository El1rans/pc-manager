using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class CleanupRunnerTests
{
    private const string Root = @"C:\Fake\Temp";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeRecycleBin _recycleBin = new();
    private readonly FakeProcessProbe _probe = new();
    private readonly FakeElevationService _elevation = new();
    private readonly FakeTimeProvider _time = new(Now);

    private static DateTime Old => Now.UtcDateTime.AddDays(-3);

    private static DateTime Young => Now.UtcDateTime.AddHours(-1);

    private CleanupRunner CreateRunner() =>
        new(_fs, _recycleBin, _probe, _elevation, _time, NullLogger<CleanupRunner>.Instance);

    private static CleanupCategory Category(
        string root = Root,
        TimeSpan? minimumAge = null,
        string? pattern = null,
        bool requiresAdmin = false,
        IReadOnlyList<CleanupRoot>? roots = null) =>
        new(
            CleanupCategoryId.TemporaryFiles, "Temp", "Temp files", requiresAdmin, true,
            roots ?? [new CleanupRoot(root)], pattern, minimumAge ?? TimeSpan.FromHours(24));

    private Task<CleanupRunResult> Clean(CleanupCategory category) =>
        CleanWith(category, TestContext.Current.CancellationToken);

    private Task<CleanupRunResult> CleanWith(CleanupCategory category, CancellationToken token) =>
        CreateRunner().CleanAsync([category], null, token);

    [Fact]
    public async Task Clean_DeletesOldFilesAndCountsBytes()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\a.tmp", 100, Old)
            .AddFile(Root + @"\b.tmp", 250, Old);

        var result = await Clean(Category());

        Assert.Equal(350, result.BytesFreed);
        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(0, result.FilesSkipped);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task Clean_FilesNewerThanMinimumAge_AreLeftAlone()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\old.tmp", 100, Old)
            .AddFile(Root + @"\young.tmp", 900, Young);

        var result = await Clean(Category());

        Assert.Equal(100, result.BytesFreed);
        Assert.True(_fs.Exists(Root + @"\young.tmp"));
        Assert.False(_fs.Exists(Root + @"\old.tmp"));
        Assert.Equal(0, result.FilesSkipped);
    }

    [Fact]
    public async Task Clean_ReparsePointDirectory_IsNeverEnteredOrDeleted()
    {
        _fs.AddDirectory(Root)
            .AddReparseDirectory(Root + @"\junction")
            .AddFile(Root + @"\junction\precious.dll", 5000, Old)
            .AddFile(Root + @"\junk.tmp", 10, Old);

        var result = await Clean(Category());

        Assert.Equal(10, result.BytesFreed);
        Assert.True(_fs.Exists(Root + @"\junction"));
        Assert.True(_fs.Exists(Root + @"\junction\precious.dll"));
        Assert.DoesNotContain(Root + @"\junction", _fs.ListedDirectories);
        Assert.DoesNotContain(Root + @"\junction", _fs.DeletedDirectories);
        Assert.DoesNotContain(_fs.DeleteFileAttempts, path => path.StartsWith(Root + @"\junction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Clean_ReparsePointFile_IsNeverDeleted()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\link.tmp", 10, Old, FileAttributes.ReparsePoint);

        var result = await Clean(Category());

        Assert.Equal(0, result.FilesDeleted);
        Assert.Empty(_fs.DeleteFileAttempts);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\evil.dll")]
    [InlineData(@"C:\Fake\Temp\..\Windows\evil.dll")]
    [InlineData(@"C:\Fake\Temp2\sibling.tmp")]
    [InlineData(@"D:\Fake\Temp\other-drive.tmp")]
    public async Task Clean_PathOutsideRoot_IsNeverDeleted(string outsidePath)
    {
        _fs.AddDirectory(Root)
            .Inject(Root, new CleanupEntry(outsidePath, Path.GetFileName(outsidePath), 999, Old, FileAttributes.Normal));

        var result = await Clean(Category());

        Assert.Empty(_fs.DeleteFileAttempts);
        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
    }

    [Fact]
    public async Task Clean_NeverDeletesTheRootItself_EvenWhenEmptied()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\a.tmp", 1, Old);

        await Clean(Category());

        Assert.True(_fs.DirectoryExists(Root));
        Assert.DoesNotContain(Root, _fs.DeletedDirectories);
    }

    [Fact]
    public async Task Clean_RemovesEmptiedDirectoriesBottomUp()
    {
        _fs.AddDirectory(Root)
            .AddDirectory(Root + @"\a")
            .AddDirectory(Root + @"\a\b")
            .AddDirectory(Root + @"\a\b\c")
            .AddFile(Root + @"\a\b\c\f.tmp", 5, Old);

        await Clean(Category());

        Assert.Equal(
            [Root + @"\a\b\c", Root + @"\a\b", Root + @"\a"],
            _fs.DeletedDirectories);
        Assert.True(_fs.DirectoryExists(Root));
    }

    [Fact]
    public async Task Clean_DirectoryStillHoldingAFile_IsKeptAlongWithItsParents()
    {
        _fs.AddDirectory(Root)
            .AddDirectory(Root + @"\keep")
            .AddDirectory(Root + @"\keep\inner")
            .AddFile(Root + @"\keep\inner\young.tmp", 1, Young)
            .AddDirectory(Root + @"\gone")
            .AddFile(Root + @"\gone\old.tmp", 1, Old);

        await Clean(Category());

        Assert.True(_fs.DirectoryExists(Root + @"\keep\inner"));
        Assert.True(_fs.DirectoryExists(Root + @"\keep"));
        Assert.False(_fs.DirectoryExists(Root + @"\gone"));
    }

    [Fact]
    public async Task Clean_EmptyDirectoryNewerThanMinimumAge_IsKept()
    {
        _fs.AddDirectory(Root)
            .AddDirectory(Root + @"\fresh", Young);

        await Clean(Category());

        Assert.True(_fs.DirectoryExists(Root + @"\fresh"));
    }

    [Fact]
    public async Task Clean_FileInUse_IsCountedSkippedAndNeverRetried()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\locked.tmp", 500, Old)
            .AddFile(Root + @"\free.tmp", 20, Old)
            .Lock(Root + @"\locked.tmp");

        var result = await Clean(Category());

        Assert.Equal(1, result.FilesSkipped);
        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(20, result.BytesFreed);
        Assert.Single(_fs.DeleteFileAttempts, path => path.EndsWith("locked.tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Clean_UnreadableDirectory_IsLeftAloneWithoutError()
    {
        _fs.AddDirectory(Root)
            .AddDirectory(Root + @"\denied")
            .AddFile(Root + @"\denied\x.tmp", 1, Old)
            .MakeUnreadable(Root + @"\denied")
            .AddFile(Root + @"\ok.tmp", 3, Old);

        var result = await Clean(Category());

        Assert.Equal(3, result.BytesFreed);
        Assert.True(_fs.DirectoryExists(Root + @"\denied"));
    }

    [Fact]
    public async Task Clean_Cancelled_StopsBetweenFilesAndReturnsPartialResult()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _fs.AddDirectory(Root);
        for (var i = 0; i < 10; i++)
        {
            _fs.AddFile(Root + $@"\f{i}.tmp", 10, Old);
        }

        _fs.AfterFileDeleted = _ => cts.Cancel();

        var result = await CleanWith(Category(), cts.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(10, result.BytesFreed);
        Assert.Equal(9, Enumerable.Range(0, 10).Count(i => _fs.Exists(Root + $@"\f{i}.tmp")));
    }

    [Fact]
    public async Task Clean_MissingRoot_IsNotAnError()
    {
        var result = await Clean(Category(root: @"C:\Fake\DoesNotExist"));

        Assert.Equal(0, result.BytesFreed);
        Assert.Equal(0, result.FilesSkipped);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task Clean_WithFilePattern_OnlyDeletesMatchingFilesDirectlyInRoot()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\thumbcache_32.db", 10, Old)
            .AddFile(Root + @"\iconcache_32.db", 20, Old)
            .AddDirectory(Root + @"\sub")
            .AddFile(Root + @"\sub\thumbcache_96.db", 30, Old);

        var result = await Clean(Category(pattern: "thumbcache_*.db"));

        Assert.Equal(10, result.BytesFreed);
        Assert.True(_fs.Exists(Root + @"\iconcache_32.db"));
        Assert.True(_fs.Exists(Root + @"\sub\thumbcache_96.db"));
        Assert.DoesNotContain(Root + @"\sub", _fs.ListedDirectories);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 1, 10)]
    public async Task Clean_AdminOnlyCategory_RunsOnlyWhenElevated(bool elevated, int expectedFilesDeleted, long expectedBytesFreed)
    {
        _fs.AddDirectory(Root).AddFile(Root + @"\a.tmp", 10, Old);
        _elevation.IsElevated = elevated;

        var result = await Clean(Category(requiresAdmin: true));

        Assert.Equal(expectedFilesDeleted, result.FilesDeleted);
        Assert.Equal(expectedBytesFreed, result.BytesFreed);
        Assert.Equal(expectedFilesDeleted, _fs.DeleteFileAttempts.Count);
    }
    [Fact]
    public async Task Clean_AdminOnlyRootInMixedCategory_IsSkippedWhenNotElevated()
    {
        const string adminRoot = @"C:\Fake\ProgramData\WER";
        _fs.AddDirectory(Root).AddFile(Root + @"\user.tmp", 1, Old)
            .AddDirectory(adminRoot).AddFile(adminRoot + @"\admin.tmp", 2, Old);
        var category = Category(roots: [new CleanupRoot(Root), new CleanupRoot(adminRoot, RequiresAdmin: true)]);

        var result = await Clean(category);

        Assert.Equal(1, result.BytesFreed);
        Assert.True(_fs.Exists(adminRoot + @"\admin.tmp"));
    }

    [Fact]
    public async Task Clean_BrowserRunning_LeavesItsCacheAloneAndSaysWhich()
    {
        const string chromeCache = @"C:\Fake\Chrome\Cache";
        const string edgeCache = @"C:\Fake\Edge\Cache";
        _fs.AddDirectory(chromeCache).AddFile(chromeCache + @"\c1", 100, Old)
            .AddDirectory(edgeCache).AddFile(edgeCache + @"\e1", 200, Old);
        _probe.SetRunning("chrome");
        var category = Category(roots:
        [
            new CleanupRoot(chromeCache, false, "chrome", "Google Chrome"),
            new CleanupRoot(edgeCache, false, "msedge", "Microsoft Edge"),
        ]);

        var result = await Clean(category);

        Assert.True(_fs.Exists(chromeCache + @"\c1"));
        Assert.False(_fs.Exists(edgeCache + @"\e1"));
        Assert.Equal(["Google Chrome"], result.BlockedPrograms);
    }

    [Fact]
    public async Task Clean_RecycleBinCategory_EmptiesTheBinAndReportsItsSize()
    {
        _recycleBin.Info = new RecycleBinInfo(4096, 7);
        var category = new CleanupCategory(
            CleanupCategoryId.RecycleBin, "Recycle Bin", "x", false, false, [], null, TimeSpan.Zero);

        var result = await Clean(category);

        Assert.Equal(1, _recycleBin.EmptyCallCount);
        Assert.Equal(4096, result.BytesFreed);
        Assert.Equal(7, result.FilesDeleted);
    }

    [Fact]
    public async Task Clean_WithoutRecycleBinCategory_NeverEmptiesTheBin()
    {
        _fs.AddDirectory(Root).AddFile(Root + @"\a.tmp", 1, Old);

        await Clean(Category());

        Assert.Equal(0, _recycleBin.EmptyCallCount);
    }

    [Fact]
    public async Task Clean_ReportsProgressAtMostEveryHundredMilliseconds()
    {
        _fs.AddDirectory(Root);
        for (var i = 0; i < 50; i++)
        {
            _fs.AddFile(Root + $@"\f{i}.tmp", 1, Old);
        }

        var reports = new List<CleanupProgress>();
        var progress = new SynchronousProgress<CleanupProgress>(reports.Add);

        await CreateRunner().CleanAsync([Category()], progress, TestContext.Current.CancellationToken);

        // The clock never advances, so only the first throttled report and the final one get through.
        Assert.InRange(reports.Count, 1, 3);
        Assert.Equal(50, reports[^1].Files);
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
