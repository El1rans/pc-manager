using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class LargeFileFinderTests
{
    private const long Mb = 1024 * 1024;
    private const string Docs = @"C:\Fake\Docs";

    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeTimeProvider _time = new(Now);

    private static DateTime Old => Now.UtcDateTime.AddDays(-100);

    private LargeFileFinder CreateFinder() => new(_fs, _time, NullLogger<LargeFileFinder>.Instance);

    private Task<IReadOnlyList<FoundFile>> Find(LargeFileSearchOptions options) =>
        CreateFinder().FindAsync(options, TestContext.Current.CancellationToken);

    private static LargeFileSearchOptions Big(params string[] roots) => LargeFileSearchOptions.BigFiles(roots, 500);

    [Fact]
    public async Task Find_ReturnsFilesAtOrAboveTheThreshold_LargestFirst()
    {
        _fs.AddDirectory(Docs)
            .AddFile(Docs + @"\small.bin", 499 * Mb, Old)
            .AddFile(Docs + @"\exact.bin", 500 * Mb, Old)
            .AddFile(Docs + @"\huge.bin", 4000 * Mb, Old)
            .AddDirectory(Docs + @"\sub")
            .AddFile(Docs + @"\sub\mid.bin", 900 * Mb, Old);

        var files = await Find(Big(Docs));

        Assert.Equal(["huge.bin", "mid.bin", "exact.bin"], files.Select(f => f.Name));
        Assert.Equal(Docs + @"\sub", files[1].Folder);
    }

    [Fact]
    public async Task Find_KeepsOnlyTheTopN()
    {
        _fs.AddDirectory(Docs);
        for (var i = 1; i <= 10; i++)
        {
            _fs.AddFile(Docs + $@"\f{i}.bin", (500 + i) * Mb, Old);
        }

        var options = Big(Docs) with { MaxResults = 3 };
        var files = await Find(options);

        Assert.Equal(["f10.bin", "f9.bin", "f8.bin"], files.Select(f => f.Name));
    }

    [Fact]
    public async Task Find_SkipsHiddenSystemReparseAndCloudOnlyFiles()
    {
        _fs.AddDirectory(Docs)
            .AddFile(Docs + @"\hidden.bin", 900 * Mb, Old, FileAttributes.Hidden)
            .AddFile(Docs + @"\system.bin", 900 * Mb, Old, FileAttributes.System)
            .AddFile(Docs + @"\link.bin", 900 * Mb, Old, FileAttributes.ReparsePoint)
            .AddFile(Docs + @"\offline.bin", 900 * Mb, Old, FileAttributes.Offline)
            .AddFile(Docs + @"\recall-data.bin", 900 * Mb, Old, (FileAttributes)0x400000)
            .AddFile(Docs + @"\recall-open.bin", 900 * Mb, Old, (FileAttributes)0x40000)
            .AddFile(Docs + @"\real.bin", 900 * Mb, Old);

        var files = await Find(Big(Docs));

        Assert.Equal(["real.bin"], files.Select(f => f.Name));
    }

    [Fact]
    public async Task Find_DoesNotEnterReparsePointOrHiddenDirectories()
    {
        _fs.AddDirectory(Docs)
            .AddReparseDirectory(Docs + @"\junction")
            .AddFile(Docs + @"\junction\x.bin", 900 * Mb, Old)
            .AddDirectory(Docs + @"\hiddenDir", null, FileAttributes.Hidden)
            .AddFile(Docs + @"\hiddenDir\y.bin", 900 * Mb, Old);

        var files = await Find(Big(Docs));

        Assert.Empty(files);
        Assert.DoesNotContain(Docs + @"\junction", _fs.ListedDirectories);
        Assert.DoesNotContain(Docs + @"\hiddenDir", _fs.ListedDirectories);
    }

    [Fact]
    public async Task Find_DuplicateAndNestedRoots_DoNotListAFileTwice()
    {
        _fs.AddDirectory(Docs)
            .AddDirectory(Docs + @"\Downloads")
            .AddFile(Docs + @"\Downloads\big.bin", 900 * Mb, Old);

        var files = await Find(Big(Docs, Docs.ToUpperInvariant() + @"\", Docs + @"\Downloads"));

        Assert.Single(files);
    }

    [Fact]
    public async Task Find_MissingRoot_IsIgnored()
    {
        var files = await Find(Big(@"C:\Fake\Missing"));

        Assert.Empty(files);
    }

    [Fact]
    public async Task Find_UnreadableDirectory_IsSkipped()
    {
        _fs.AddDirectory(Docs)
            .AddDirectory(Docs + @"\locked")
            .MakeUnreadable(Docs + @"\locked")
            .AddFile(Docs + @"\ok.bin", 900 * Mb, Old);

        var files = await Find(Big(Docs));

        Assert.Equal(["ok.bin"], files.Select(f => f.Name));
    }

    [Fact]
    public async Task OldDownloads_OnlyInstallersAndArchivesOlderThan30Days_AtLeast10Mb()
    {
        const string downloads = @"C:\Fake\Downloads";
        var recent = Now.UtcDateTime.AddDays(-5);
        var older = Now.UtcDateTime.AddDays(-45);
        _fs.AddDirectory(downloads)
            .AddFile(downloads + @"\setup.exe", 50 * Mb, older)
            .AddFile(downloads + @"\Pack.ZIP", 20 * Mb, older)
            .AddFile(downloads + @"\image.iso", 3000 * Mb, older)
            .AddFile(downloads + @"\tiny.exe", 9 * Mb, older)
            .AddFile(downloads + @"\recent.exe", 50 * Mb, recent)
            .AddFile(downloads + @"\movie.mp4", 900 * Mb, older)
            .AddFile(downloads + @"\notes.docx", 50 * Mb, older);

        var files = await Find(LargeFileSearchOptions.OldDownloads(downloads));

        Assert.Equal(["image.iso", "setup.exe", "Pack.ZIP"], files.Select(f => f.Name));
    }

    [Fact]
    public async Task Find_Cancelled_Throws()
    {
        _fs.AddDirectory(Docs);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateFinder().FindAsync(Big(Docs), cts.Token));
    }

    [Fact]
    public async Task Find_NeverDeletesAnything()
    {
        _fs.AddDirectory(Docs).AddFile(Docs + @"\big.bin", 900 * Mb, Old);

        await Find(Big(Docs));

        Assert.Empty(_fs.DeleteFileAttempts);
    }
}
