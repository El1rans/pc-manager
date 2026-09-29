namespace Porchlight.Core.Cleanup;

/// <summary>Finds byte-identical files in the user's personal folders. Read-only.</summary>
public interface IDuplicateFinder
{
    /// <summary>Runs on a background thread. Returns groups with the most wasted space first. Throws
    /// <see cref="OperationCanceledException"/> if cancelled.</summary>
    Task<IReadOnlyList<DuplicateGroup>> FindAsync(
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken);
}
