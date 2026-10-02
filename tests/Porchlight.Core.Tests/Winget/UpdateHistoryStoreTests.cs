using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class UpdateHistoryStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public UpdateHistoryStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightHistoryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, UpdateHistoryStore.FileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private UpdateHistoryStore CreateStore() => new(NullLogger<UpdateHistoryStore>.Instance, _path);

    private static UpdateHistoryEntry Entry(string id = "A.Id") => new()
    {
        TimestampUtc = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero),
        PackageId = id,
        PackageName = "App " + id,
        FromVersion = "1.0",
        ToVersion = "2.0",
        Action = UpdateHistoryAction.Reinstall,
        Succeeded = false,
        OutcomeTitle = "Needs a reinstall",
        Explanation = "Because.",
        ExitCode = unchecked((int)0x8A15008E),
    };

    [Fact]
    public void GetAll_MissingFile_IsEmpty()
    {
        Assert.Empty(CreateStore().GetAll());
    }

    [Fact]
    public void Add_RoundTripsThroughANewStore()
    {
        CreateStore().Add(Entry());

        var loaded = Assert.Single(CreateStore().GetAll());
        Assert.Equal("A.Id", loaded.PackageId);
        Assert.Equal("1.0", loaded.FromVersion);
        Assert.Equal("2.0", loaded.ToVersion);
        Assert.Equal(UpdateHistoryAction.Reinstall, loaded.Action);
        Assert.False(loaded.Succeeded);
        Assert.Equal("Needs a reinstall", loaded.OutcomeTitle);
        Assert.Equal(unchecked((int)0x8A15008E), loaded.ExitCode);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero), loaded.TimestampUtc);
    }

    [Fact]
    public void Add_WritesAtomically_LeavingNoTempFile()
    {
        CreateStore().Add(Entry());

        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public void Add_CapsAtMaxEntries()
    {
        var store = CreateStore();
        for (var i = 0; i < UpdateHistoryLog.MaxEntries + 2; i++)
        {
            store.Add(Entry("Pkg" + i));
        }

        var all = CreateStore().GetAll();
        Assert.Equal(UpdateHistoryLog.MaxEntries, all.Count);
        Assert.Equal("Pkg2", all[0].PackageId);
    }

    [Fact]
    public void CorruptFile_IsRenamedToBad_AndHistoryStartsEmpty()
    {
        File.WriteAllText(_path, "{ not json");
        File.WriteAllText(_path + ".bad", "older bad file");

        var store = CreateStore();

        Assert.Empty(store.GetAll());
        Assert.False(File.Exists(_path));
        Assert.Equal("{ not json", File.ReadAllText(_path + ".bad"));

        store.Add(Entry());
        Assert.Single(CreateStore().GetAll());
    }

    [Fact]
    public void Clear_RemovesEverythingIncludingOnDisk()
    {
        var store = CreateStore();
        store.Add(Entry());

        store.Clear();

        Assert.Empty(store.GetAll());
        Assert.Empty(CreateStore().GetAll());
    }

    [Fact]
    public void WriteFailure_KeepsTheEntryInMemory()
    {
        // The target path is a directory, so the final move fails; the in-memory list must survive.
        Directory.CreateDirectory(_path);
        var store = CreateStore();

        store.Add(Entry());

        Assert.Single(store.GetAll());
    }
}
