namespace Porchlight.Core.Cleanup;

/// <summary>Finds big or old personal files to suggest to the user. Read-only.</summary>
public interface ILargeFileFinder
{
    /// <summary>Runs on a background thread. Returns at most <see cref="LargeFileSearchOptions.MaxResults"/>
    /// files, largest first. Throws <see cref="OperationCanceledException"/> if cancelled.</summary>
    Task<IReadOnlyList<FoundFile>> FindAsync(LargeFileSearchOptions options, CancellationToken cancellationToken);
}
