using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class DuplicateFinderTests
{
    private const string Docs = @"C:\Fake\Docs";
    private const int Partial = DuplicateFinder.PartialBytes;
    private const int Size = 300 * 1024; // bigger than first+last 64 KB, so the quick check skips the middle.

    private static readonly DateTime Old = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeFileContentReader _reader = new();

    public DuplicateFinderTests()
    {
        _fs.AddDirectory(Docs);
    }

    private static byte[] Bytes(int length, byte fill = 7) => Enumerable.Repeat(fill, length).ToArray();

    private static byte[] With(byte[] source, int index, byte value)
    {
        var copy = (byte[])source.Clone();
        copy[index] = value;
        return copy;
    }

    private void Add(string path, byte[] content, DateTime? modified = null, FileAttributes attributes = FileAttributes.Normal)
    {
        _fs.AddFile(path, content.Length, modified ?? Old, attributes);
        _reader.Add(path, content);
    }

    private Task<IReadOnlyList<DuplicateGroup>> Find(
        DuplicateSearchOptions? options = null, IProgress<DuplicateProgress>? progress = null) =>
        FindWith(TestContext.Current.CancellationToken, options, progress);

    private Task<IReadOnlyList<DuplicateGroup>> FindWith(
        CancellationToken token, DuplicateSearchOptions? options = null, IProgress<DuplicateProgress>? progress = null) =>
        new DuplicateFinder(_fs, _reader, NullLogger<DuplicateFinder>.Instance)
            .FindAsync(options ?? new DuplicateSearchOptions([Docs], MinimumBytes: 1), progress, token);

    [Fact]
    public async Task Find_FilesOfDifferentSizes_AreNeverRead()
    {
        Add(Docs + @"\a.bin", Bytes(Size));
        Add(Docs + @"\b.bin", Bytes(Size + 1));
        Add(Docs + @"\c.bin", Bytes(Size + 2));

        var groups = await Find();

        Assert.Empty(groups);
        Assert.Empty(_reader.Opened);
    }

    [Fact]
    public async Task Find_SameSizeButDifferentStart_IsRejectedByTheQuickCheck_WithoutReadingWholeFiles()
    {
        Add(Docs + @"\a.bin", Bytes(Size));
        Add(Docs + @"\b.bin", With(Bytes(Size), 0, 9));

        var groups = await Find();

        Assert.Empty(groups);
        Assert.Equal(2 * Partial, _reader.BytesRead[Docs + @"\a.bin"]);
        Assert.Equal(2 * Partial, _reader.BytesRead[Docs + @"\b.bin"]);
    }

    [Fact]
    public async Task Find_SameSizeButDifferentEnd_IsRejectedByTheQuickCheck()
    {
        Add(Docs + @"\a.bin", Bytes(Size));
        Add(Docs + @"\b.bin", With(Bytes(Size), Size - 1, 9));

        Assert.Empty(await Find());
        Assert.Equal(2 * Partial, _reader.TotalBytesRead / 2);
    }

    [Fact]
    public async Task Find_SameStartAndEndButDifferentMiddle_IsRejectedOnlyByTheFullHash()
    {
        Add(Docs + @"\a.bin", Bytes(Size));
        Add(Docs + @"\b.bin", With(Bytes(Size), Size / 2, 9));

        var groups = await Find();

        Assert.Empty(groups);
        // Quick check (128 KB) passed, so each file was then read in full.
        Assert.Equal(2 * Partial + Size, _reader.BytesRead[Docs + @"\a.bin"]);
        Assert.Equal(2 * Partial + Size, _reader.BytesRead[Docs + @"\b.bin"]);
    }

    [Fact]
    public async Task Find_IdenticalFiles_AreGrouped_AndOnlyQuickCheckSurvivorsAreReadInFull()
    {
        Add(Docs + @"\a.bin", Bytes(Size), Old);
        Add(Docs + @"\copy.bin", Bytes(Size), Old.AddDays(5));
        Add(Docs + @"\odd.bin", With(Bytes(Size), 10, 3), Old);

        var groups = await Find();

        var group = Assert.Single(groups);
        Assert.Equal(["copy.bin", "a.bin"], group.Files.Select(f => f.Name)); // newest first
        Assert.Equal(Size, group.FileBytes);
        Assert.Equal(Size, group.WastedBytes);
        Assert.Equal(2 * Partial, _reader.BytesRead[Docs + @"\odd.bin"]);
        Assert.Equal(2 * Partial + Size, _reader.BytesRead[Docs + @"\a.bin"]);
    }

    [Fact]
    public async Task Find_SmallFile_IsReadOnceInTheQuickCheckAndOnceInFull()
    {
        const int small = 100 * 1024; // <= 128 KB: the quick check reads the whole file.
        Add(Docs + @"\a.bin", Bytes(small));
        Add(Docs + @"\b.bin", Bytes(small));

        var groups = await Find();

        Assert.Single(groups);
        Assert.Equal(2 * small, _reader.BytesRead[Docs + @"\a.bin"]);
    }

    [Fact]
    public async Task Find_WastedSpaceIsSizeTimesExtraCopies_AndGroupsAreSortedByIt()
    {
        Add(Docs + @"\small1.bin", Bytes(1_000, 1));
        Add(Docs + @"\small2.bin", Bytes(1_000, 1));
        Add(Docs + @"\big1.bin", Bytes(5_000, 2));
        Add(Docs + @"\big2.bin", Bytes(5_000, 2));
        Add(Docs + @"\big3.bin", Bytes(5_000, 2));

        var groups = await Find();

        Assert.Equal([10_000L, 1_000L], groups.Select(g => g.WastedBytes));
        Assert.Equal(3, groups[0].Files.Count);
    }

    [Fact]
    public async Task Find_SkipsHiddenSystemReparseCloudAndTooSmallFiles()
    {
        Add(Docs + @"\a.bin", Bytes(2_000));
        Add(Docs + @"\hidden.bin", Bytes(2_000), attributes: FileAttributes.Hidden);
        Add(Docs + @"\system.bin", Bytes(2_000), attributes: FileAttributes.System);
        Add(Docs + @"\cloud.bin", Bytes(2_000), attributes: FileAttributes.Offline);
        Add(Docs + @"\recall.bin", Bytes(2_000), attributes: (FileAttributes)0x00400000);
        Add(Docs + @"\link.bin", Bytes(2_000), attributes: FileAttributes.ReparsePoint);
        Add(Docs + @"\tiny1.bin", Bytes(10, 5));
        Add(Docs + @"\tiny2.bin", Bytes(10, 5));
        _fs.AddReparseDirectory(Docs + @"\Junction");
        Add(Docs + @"\Junction\dup.bin", Bytes(2_000));
        _fs.AddDirectory(Docs + @"\Hidden", extra: FileAttributes.Hidden);
        Add(Docs + @"\Hidden\dup.bin", Bytes(2_000));

        var groups = await Find(new DuplicateSearchOptions([Docs], MinimumBytes: 1_000));

        Assert.Empty(groups);
        Assert.Empty(_reader.Opened);
    }

    [Fact]
    public async Task Find_DefaultMinimumIsOneMegabyte()
    {
        var mb = 1024 * 1024;
        Add(Docs + @"\under1.bin", Bytes(mb - 1));
        Add(Docs + @"\under2.bin", Bytes(mb - 1));
        Add(Docs + @"\exact1.bin", Bytes(mb));
        Add(Docs + @"\exact2.bin", Bytes(mb));

        var groups = await Find(new DuplicateSearchOptions([Docs]));

        Assert.Equal(mb, Assert.Single(groups).FileBytes);
    }

    [Fact]
    public async Task Find_AFileThatCannotBeRead_IsLeftOut_AndTheScanContinues()
    {
        Add(Docs + @"\a.bin", Bytes(2_000));
        Add(Docs + @"\b.bin", Bytes(2_000));
        Add(Docs + @"\locked.bin", Bytes(2_000));
        _reader.Lock(Docs + @"\locked.bin");

        var group = Assert.Single(await Find());

        Assert.Equal(["a.bin", "b.bin"], group.Files.Select(f => f.Name).Order());
    }

    [Fact]
    public async Task Find_DoesNotListAFileTwiceWhenRootsOverlap()
    {
        _fs.AddDirectory(Docs + @"\Sub");
        Add(Docs + @"\a.bin", Bytes(2_000));
        Add(Docs + @"\Sub\b.bin", Bytes(2_000));

        var groups = await Find(new DuplicateSearchOptions([Docs, Docs + @"\Sub"], MinimumBytes: 1));

        Assert.Equal(2, Assert.Single(groups).Files.Count);
    }

    [Fact]
    public async Task Find_KeepsOnlyMaxGroups()
    {
        for (var i = 0; i < 3; i++)
        {
            Add(Docs + $@"\g{i}a.bin", Bytes(1_000 + i, (byte)i));
            Add(Docs + $@"\g{i}b.bin", Bytes(1_000 + i, (byte)i));
        }

        var groups = await Find(new DuplicateSearchOptions([Docs], MinimumBytes: 1, MaxGroups: 2));

        Assert.Equal(2, groups.Count);
        Assert.Equal(1_002, groups[0].FileBytes);
    }

    [Fact]
    public async Task Find_CancelledDuringTheFullHash_Throws()
    {
        Add(Docs + @"\a.bin", Bytes(Size));
        Add(Docs + @"\b.bin", Bytes(Size));
        using var cts = new CancellationTokenSource();
        // Cancel once the quick check is over and a whole file is being read.
        _reader.OnRead = path =>
        {
            if (_reader.BytesRead.GetValueOrDefault(path) >= 2 * Partial)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FindWith(cts.Token));
    }

    [Fact]
    public async Task Find_AlreadyCancelled_ReadsNothing()
    {
        Add(Docs + @"\a.bin", Bytes(2_000));
        Add(Docs + @"\b.bin", Bytes(2_000));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FindWith(cts.Token));
        Assert.Empty(_reader.Opened);
    }

    [Fact]
    public async Task Find_ReportsProgressStartingWithTheListingStage()
    {
        Add(Docs + @"\a.bin", Bytes(2_000));
        Add(Docs + @"\b.bin", Bytes(2_000));
        var reports = new List<DuplicateProgress>();

        await Find(progress: new SyncProgress(reports));

        Assert.NotEmpty(reports);
        Assert.Equal(DuplicateStage.Listing, reports[0].Stage);
    }

    private sealed class SyncProgress(List<DuplicateProgress> reports) : IProgress<DuplicateProgress>
    {
        public void Report(DuplicateProgress value) => reports.Add(value);
    }
}
