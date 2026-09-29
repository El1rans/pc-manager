namespace Porchlight.Core.Cleanup;

/// <summary>Measures how much space each folder uses. Read-only.</summary>
public interface IDiskSpaceMapper
{
    /// <summary>Walks <paramref name="root"/> on a background thread and returns its folder tree with
    /// sizes. Never follows reparse points; folders that cannot be read are skipped and counted in
    /// <see cref="DiskNode.UnreadableFolders"/>. Throws <see cref="DirectoryNotFoundException"/> if the
    /// root does not exist and <see cref="OperationCanceledException"/> if cancelled.</summary>
    Task<DiskNode> MapAsync(string root, IProgress<DiskMapProgress>? progress, CancellationToken cancellationToken);
}
