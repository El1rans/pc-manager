using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class CleanupScannerTests
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

    private CleanupScanner CreateScanner() =>
        new(_fs, _recycleBin, _probe, _elevation, _time, NullLogger<CleanupScanner>.Instance);

    private static CleanupCategory Category(
        IReadOnlyList<CleanupRoot>? roots = null, bool requiresAdmin = false, string? pattern = null) =>
        new(
            CleanupCategoryId.TemporaryFiles, "Temp", "Temp files", requiresAdmin, true,
            roots ?? [new CleanupRoot(Root)], pattern, TimeSpan.FromHours(24));

    private async Task<CleanupCategoryScan> Scan(CleanupCategory category)
    {
        var result = await CreateScanner().ScanAsync([category], null, TestContext.Current.CancellationToken);
        return Assert.Single(result.Categories);
    }

    [Fact]
    public async Task Scan_SumsOnlyFilesOldEnough()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\a.tmp", 100, Old)
            .AddFile(Root + @"\young.tmp", 5000, Young)
            .AddDirectory(Root + @"\sub")
            .AddFile(Root + @"\sub\b.tmp", 50, Old);

        var scan = await Scan(Category());

        Assert.Equal(150, scan.Bytes);
        Assert.Equal(2, scan.FileCount);
    }

    [Fact]
    public async Task Scan_ReparsePoints_AreNeitherEnteredNorCounted()
    {
        _fs.AddDirectory(Root)
            .AddReparseDirectory(Root + @"\junction")
            .AddFile(Root + @"\junction\big.bin", 1_000_000, Old)
            .AddFile(Root + @"\link.tmp", 777, Old, FileAttributes.ReparsePoint)
            .AddFile(Root + @"\real.tmp", 10, Old);

        var scan = await Scan(Category());

        Assert.Equal(10, scan.Bytes);
        Assert.DoesNotContain(Root + @"\junction", _fs.ListedDirectories);
    }

    [Fact]
    public async Task Scan_MissingRoot_IsZeroNotAnError()
    {
        var scan = await Scan(Category([new CleanupRoot(@"C:\Fake\Nope")]));

        Assert.Equal(0, scan.Bytes);
    }

    [Fact]
    public async Task Scan_AdminOnlyCategory_IsZeroWhenNotElevated()
    {
        _fs.AddDirectory(Root).AddFile(Root + @"\a.tmp", 100, Old);

        var scan = await Scan(Category(requiresAdmin: true));

        Assert.Equal(0, scan.Bytes);
    }

    [Fact]
    public async Task Scan_RunningBrowser_IsReportedAndItsCacheNotCounted()
    {
        const string cache = @"C:\Fake\Chrome\Cache";
        _fs.AddDirectory(cache).AddFile(cache + @"\c", 100, Old);
        _probe.SetRunning("chrome");

        var scan = await Scan(Category([new CleanupRoot(cache, false, "chrome", "Google Chrome")]));

        Assert.Equal(0, scan.Bytes);
        Assert.Equal(["Google Chrome"], scan.BlockedPrograms);
    }

    [Fact]
    public async Task Scan_FilePattern_CountsOnlyMatchingTopLevelFiles()
    {
        _fs.AddDirectory(Root)
            .AddFile(Root + @"\thumbcache_16.db", 10, Old)
            .AddFile(Root + @"\other.db", 99, Old)
            .AddDirectory(Root + @"\sub")
            .AddFile(Root + @"\sub\thumbcache_32.db", 55, Old);

        var scan = await Scan(Category(pattern: "thumbcache_*.db"));

        Assert.Equal(10, scan.Bytes);
    }

    [Fact]
    public async Task Scan_RecycleBin_UsesTheShellSize()
    {
        _recycleBin.Info = new RecycleBinInfo(2048, 3);
        var category = new CleanupCategory(
            CleanupCategoryId.RecycleBin, "Recycle Bin", "x", false, false, [], null, TimeSpan.Zero);

        var scan = await Scan(category);

        Assert.Equal(2048, scan.Bytes);
        Assert.Equal(3, scan.FileCount);
    }

    [Fact]
    public async Task Scan_Cancelled_Throws()
    {
        _fs.AddDirectory(Root).AddFile(Root + @"\a.tmp", 1, Old);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateScanner().ScanAsync([Category()], null, cts.Token));
    }

    [Fact]
    public async Task Scan_NeverDeletesAnything()
    {
        _fs.AddDirectory(Root).AddFile(Root + @"\a.tmp", 1, Old);

        await Scan(Category());

        Assert.Empty(_fs.DeleteFileAttempts);
        Assert.Empty(_fs.DeletedDirectories);
    }
}
