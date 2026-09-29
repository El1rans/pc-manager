namespace Porchlight.Core.Cleanup;

/// <summary>Moves chosen duplicate copies to the Recycle Bin, always leaving at least one copy of
/// every group.</summary>
public interface IDuplicateRemover
{
    /// <summary>Runs on a background thread. Only paths that are copies inside <paramref name="groups"/>
    /// are ever touched; if the selection covers every copy of a group the newest is spared; a group
    /// whose surviving copy cannot be confirmed on disk is skipped. Cancelling stops between files
    /// and returns what was done so far.</summary>
    Task<DuplicateRemoveResult> RemoveAsync(
        IReadOnlyList<DuplicateGroup> groups, IReadOnlyCollection<string> selectedPaths, CancellationToken cancellationToken);
}
