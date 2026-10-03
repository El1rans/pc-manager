using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Changes;
using Xunit;

namespace Porchlight.Core.Tests.Changes;

public sealed class ChangeJournalTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "porchlight-journal-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeUndoer _undoer = new("fake.type");

    private string JournalPath => Path.Combine(_folder, ChangeJournal.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ChangeJournal Create() =>
        new(NullLogger<ChangeJournal>.Instance, [_undoer], _time, JournalPath);

    [Fact]
    public void Record_ListsNewestFirst()
    {
        var journal = Create();

        journal.Record(ChangeArea.Startup, "first");
        _time.Advance(TimeSpan.FromMinutes(1));
        journal.Record(ChangeArea.Services, "second");

        Assert.Equal(["second", "first"], journal.GetAll().Select(e => e.Description));
    }

    [Fact]
    public void Record_RaisesChanged()
    {
        var journal = Create();
        var raised = 0;
        journal.Changed += (_, _) => raised++;

        journal.Record(ChangeArea.Apps, "Removed Contoso Player");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Entries_SurviveANewInstance()
    {
        Create().Record(ChangeArea.Cleanup, "Cleared 1 GB", "fake.type", "{\"a\":1}");

        var entry = Assert.Single(Create().GetAll());

        Assert.Equal("Cleared 1 GB", entry.Description);
        Assert.Equal(ChangeArea.Cleanup, entry.Area);
        Assert.Equal("fake.type", entry.UndoType);
        Assert.Equal("{\"a\":1}", entry.UndoPayload);
        Assert.True(entry.CanUndo);
    }

    [Fact]
    public void Record_CapsAtMaxEntriesDroppingTheOldest()
    {
        var journal = Create();

        for (var i = 0; i < ChangeJournalRules.MaxEntries + 5; i++)
        {
            journal.Record(ChangeArea.Updates, $"change {i}");
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var all = journal.GetAll();
        Assert.Equal(ChangeJournalRules.MaxEntries, all.Count);
        Assert.Equal($"change {ChangeJournalRules.MaxEntries + 4}", all[0].Description);
        Assert.Equal("change 5", all[^1].Description);
    }

    [Fact]
    public void Record_DropsEntriesOlderThanTheAgeLimit()
    {
        var journal = Create();
        journal.Record(ChangeArea.Updates, "old");
        _time.Advance(TimeSpan.FromDays(ChangeJournalRules.MaxAgeDays + 1));

        journal.Record(ChangeArea.Updates, "new");

        Assert.Equal(["new"], journal.GetAll().Select(e => e.Description));
    }

    [Fact]
    public void CorruptFile_StartsEmptyAndIsMovedAside()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(JournalPath, "{ this is not json");

        var journal = Create();

        Assert.Empty(journal.GetAll());
        Assert.True(File.Exists(JournalPath + ".bad"));
        journal.Record(ChangeArea.Startup, "works again");
        Assert.Single(Create().GetAll());
    }

    [Fact]
    public async Task Undo_RunsTheUndoerWithThePayloadAndMarksTheEntry()
    {
        var journal = Create();
        journal.Record(ChangeArea.Startup, "Turned off Foo", "fake.type", "payload-1");
        var id = journal.GetAll()[0].Id;

        var result = await journal.UndoAsync(id, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["payload-1"], _undoer.Payloads);
        var entry = journal.GetAll()[0];
        Assert.NotNull(entry.UndoneAt);
        Assert.False(entry.CanUndo);

        var again = await journal.UndoAsync(id, TestContext.Current.CancellationToken);
        Assert.False(again.Succeeded);
        Assert.Single(_undoer.Payloads);
    }

    [Fact]
    public async Task Undo_FailureLeavesTheEntryUndoable()
    {
        var journal = Create();
        _undoer.Result = ChangeUndoResult.Fail("Needs admin.");
        journal.Record(ChangeArea.Services, "Stopped Foo", "fake.type", "p");
        var id = journal.GetAll()[0].Id;

        var result = await journal.UndoAsync(id, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Needs admin.", result.Message);
        Assert.True(journal.GetAll()[0].CanUndo);
    }

    [Fact]
    public async Task Undo_ThrowingUndoerIsReportedInPlainWords()
    {
        var journal = Create();
        _undoer.Throw = true;
        journal.Record(ChangeArea.Services, "Stopped Foo", "fake.type", "p");

        var result = await journal.UndoAsync(journal.GetAll()[0].Id, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(journal.GetAll()[0].CanUndo);
    }

    [Fact]
    public async Task Undo_EntryWithoutUndoUnknownTypeOrUnknownIdIsRefused()
    {
        var journal = Create();
        journal.Record(ChangeArea.Cleanup, "Cleared 1 GB");
        journal.Record(ChangeArea.Services, "Mystery", "no.such.type", "p");
        var all = journal.GetAll();

        Assert.False((await journal.UndoAsync(all[1].Id, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await journal.UndoAsync(all[0].Id, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await journal.UndoAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Empty(_undoer.Payloads);
        Assert.False(all[1].CanUndo);
    }

    [Fact]
    public async Task Undo_PicksTheUndoerByType()
    {
        var other = new FakeUndoer("other.type");
        var journal = new ChangeJournal(NullLogger<ChangeJournal>.Instance, [_undoer, other], _time, JournalPath);
        journal.Record(ChangeArea.Startup, "a", "other.type", "x");

        await journal.UndoAsync(journal.GetAll()[0].Id, TestContext.Current.CancellationToken);

        Assert.Empty(_undoer.Payloads);
        Assert.Equal(["x"], other.Payloads);
    }

    private sealed class FakeUndoer(string type) : IChangeUndoer
    {
        public string UndoType => type;

        public List<string> Payloads { get; } = [];

        public ChangeUndoResult Result { get; set; } = ChangeUndoResult.Ok("Done.");

        public bool Throw { get; set; }

        public Task<ChangeUndoResult> UndoAsync(string payload, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new InvalidOperationException("boom");
            }

            Payloads.Add(payload);
            return Task.FromResult(Result);
        }
    }
}
