#if DEBUG
namespace Porchlight.Core.Changes.Demo;

/// <summary>DEBUG demo data: a made-up in-memory journal; "undo" only marks the entry undone.</summary>
internal sealed class DemoChangeJournal : IChangeJournal
{
    private readonly Lock _lock = new();
    private readonly List<ChangeEntry> _entries; // oldest first

    public DemoChangeJournal()
    {
        var now = DateTimeOffset.UtcNow;
        _entries =
        [
            new(Guid.NewGuid(), now.AddDays(-3), ChangeArea.Cleanup, "Cleared 1.2 GB of temporary files", null, null, null),
            new(Guid.NewGuid(), now.AddDays(-2), ChangeArea.Updates, "Updated Sample Browser", null, null, null),
            new(Guid.NewGuid(), now.AddHours(-20), ChangeArea.Startup, "Turned off Sample Chat at startup", "demo", "{}", null),
            new(Guid.NewGuid(), now.AddHours(-3), ChangeArea.Services, "Set Photo Sync Agent to \"Starts when needed\"", "demo", "{}", null),
        ];
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ChangeEntry> GetAll()
    {
        lock (_lock)
        {
            return [.. _entries.AsEnumerable().Reverse()];
        }
    }

    public void Record(ChangeArea area, string description, string? undoType = null, string? undoPayload = null)
    {
        lock (_lock)
        {
            _entries.Add(new ChangeEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, area, description, undoType, undoPayload, null));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<ChangeUndoResult> UndoAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var index = _entries.FindIndex(e => e.Id == id && e.CanUndo);
            if (index < 0)
            {
                return Task.FromResult(ChangeUndoResult.Fail("This change can't be undone."));
            }

            _entries[index] = _entries[index] with { UndoneAt = DateTimeOffset.UtcNow };
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(ChangeUndoResult.Ok("Done. This is back the way it was."));
    }
}
#endif
