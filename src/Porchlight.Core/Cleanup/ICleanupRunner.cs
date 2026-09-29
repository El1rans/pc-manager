namespace Porchlight.Core.Cleanup;

/// <summary>Deletes the contents of the chosen cleanup categories, following the safety rules in
/// docs/specs/12-disk-cleanup.md.</summary>
public interface ICleanupRunner
{
    /// <summary>Runs entirely on a background thread. Cancelling stops between files and returns
    /// what was done so far with <see cref="CleanupRunResult.WasCancelled"/> set (it does not throw).</summary>
    Task<CleanupRunResult> CleanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken);
}
