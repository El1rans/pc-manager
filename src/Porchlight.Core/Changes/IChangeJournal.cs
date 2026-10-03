namespace Porchlight.Core.Changes;

/// <summary>The "Recent changes" log: what Porchlight changed on this PC and, where possible, how to
/// undo it. See docs/specs/34-recent-changes.md. A feature records a change with one call, e.g.
/// <c>journal.Record(ChangeArea.Apps, "Removed Contoso Player")</c>.</summary>
public interface IChangeJournal
{
    /// <summary>Raised after an entry is recorded or undone (may be on any thread).</summary>
    event EventHandler? Changed;

    /// <summary>All entries, newest first.</summary>
    IReadOnlyList<ChangeEntry> GetAll();

    /// <summary>Appends an entry stamped now. Pass <paramref name="undoType"/> and
    /// <paramref name="undoPayload"/> only when an <see cref="IChangeUndoer"/> can reverse it.
    /// Never throws; a journal that can't be saved is kept in memory.</summary>
    void Record(ChangeArea area, string description, string? undoType = null, string? undoPayload = null);

    /// <summary>Undoes the entry with <paramref name="id"/> and, on success, marks it undone.</summary>
    Task<ChangeUndoResult> UndoAsync(Guid id, CancellationToken cancellationToken);
}
