using Porchlight.Core.Changes;

namespace Porchlight.App.Tests.TestDoubles;

/// <summary>In-memory <see cref="IChangeJournal"/> that remembers what was recorded.</summary>
internal sealed class FakeJournal : IChangeJournal
{
    public List<ChangeEntry> Recorded { get; } = [];

    public ChangeUndoResult UndoResult { get; set; } = ChangeUndoResult.Ok("Done.");

    public event EventHandler? Changed;

    public IReadOnlyList<ChangeEntry> GetAll() => [.. Recorded.AsEnumerable().Reverse()];

    public void Record(ChangeArea area, string description, string? undoType = null, string? undoPayload = null)
    {
        Recorded.Add(new ChangeEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, area, description, undoType, undoPayload, null));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<ChangeUndoResult> UndoAsync(Guid id, CancellationToken cancellationToken)
    {
        var index = Recorded.FindIndex(e => e.Id == id);
        if (index >= 0 && UndoResult.Succeeded)
        {
            Recorded[index] = Recorded[index] with { UndoneAt = DateTimeOffset.UtcNow };
        }

        return Task.FromResult(UndoResult);
    }
}
