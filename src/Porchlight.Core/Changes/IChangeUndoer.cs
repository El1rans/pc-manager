namespace Porchlight.Core.Changes;

/// <summary>Reverses one kind of recorded change. Each feature owns its undoers and registers them
/// as <see cref="IChangeUndoer"/> singletons; the journal picks one by <see cref="UndoType"/>.</summary>
public interface IChangeUndoer
{
    /// <summary>The key stored in <see cref="ChangeEntry.UndoType"/>, e.g. "startup.enabled".</summary>
    string UndoType { get; }

    /// <summary>Reverses the change described by <paramref name="payload"/>. Never throws for an
    /// ordinary failure; reports it as a failed result in plain words.</summary>
    Task<ChangeUndoResult> UndoAsync(string payload, CancellationToken cancellationToken);
}
