using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IDuplicateRemover"/>
public sealed partial class DuplicateRemover : IDuplicateRemover
{
    private readonly ICleanupFileSystem _fileSystem;
    private readonly IRecycler _recycler;
    private readonly ILogger<DuplicateRemover> _logger;

    public DuplicateRemover(ICleanupFileSystem fileSystem, IRecycler recycler, ILogger<DuplicateRemover> logger)
    {
        _fileSystem = fileSystem;
        _recycler = recycler;
        _logger = logger;
    }

    public Task<DuplicateRemoveResult> RemoveAsync(
        IReadOnlyList<DuplicateGroup> groups, IReadOnlyCollection<string> selectedPaths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(selectedPaths);
        return Task.Run(() => Remove(groups, selectedPaths, cancellationToken), CancellationToken.None);
    }

    private DuplicateRemoveResult Remove(
        IReadOnlyList<DuplicateGroup> groups, IReadOnlyCollection<string> selectedPaths, CancellationToken cancellationToken)
    {
        var selected = new HashSet<string>(selectedPaths, StringComparer.OrdinalIgnoreCase);
        var folderCache = new Dictionary<string, IReadOnlyList<CleanupEntry>?>(StringComparer.OrdinalIgnoreCase);
        int failed = 0, changed = 0, keptOne = 0;
        var removedPaths = new List<string>();
        long freed = 0;

        foreach (var group in groups)
        {
            // Rule 1: only copies that belong to a group are ever considered.
            var toRemove = group.Files.Where(file => selected.Contains(file.FullPath)).ToList();
            if (toRemove.Count == 0)
            {
                continue;
            }

            // Rule 2: never remove every copy; the newest is spared.
            var survivors = group.Files.Except(toRemove).ToList();
            if (survivors.Count == 0)
            {
                var spared = DuplicateSelection.PickNewest(group);
                toRemove.Remove(spared);
                survivors.Add(spared);
                keptOne++;
                if (toRemove.Count == 0)
                {
                    continue;
                }
            }

            // Rule 3: at least one copy that will stay must still be there, unchanged.
            if (!survivors.Any(file => IsUnchanged(file, group.FileBytes, folderCache)))
            {
                changed += toRemove.Count;
                LogSurvivorMissing(group.Files[0].Name);
                continue;
            }

            foreach (var file in toRemove)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new DuplicateRemoveResult(removedPaths.Count, freed, failed, changed, keptOne, WasCancelled: true, removedPaths);
                }

                // Rule 4: a copy that changed since the scan may no longer be a duplicate.
                if (!IsUnchanged(file, group.FileBytes, folderCache))
                {
                    changed++;
                    continue;
                }

                // Rule 5: Recycle Bin only; a refusal is final.
                if (_recycler.MoveToRecycleBin(file.FullPath))
                {
                    removedPaths.Add(file.FullPath);
                    freed += group.FileBytes;
                }
                else
                {
                    failed++;
                }
            }
        }

        return new DuplicateRemoveResult(removedPaths.Count, freed, failed, changed, keptOne, WasCancelled: false, removedPaths);
    }

    private bool IsUnchanged(DuplicateFile file, long bytes, Dictionary<string, IReadOnlyList<CleanupEntry>?> cache)
    {
        if (!cache.TryGetValue(file.Folder, out var entries))
        {
            try
            {
                entries = _fileSystem.EnumerateEntries(file.Folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogFolderUnreadable(ex, file.Folder);
                entries = null;
            }

            cache[file.Folder] = entries;
        }

        return entries is not null && entries.Any(entry =>
            !entry.IsDirectory &&
            !entry.IsReparsePoint &&
            string.Equals(entry.FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase) &&
            entry.Length == bytes &&
            entry.LastWriteUtc == file.LastWriteUtc);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped a duplicate group ({Name}): no copy to keep could be confirmed on disk.")]
    private partial void LogSurvivorMissing(string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not re-check folder {Folder} before removing duplicates.")]
    private partial void LogFolderUnreadable(Exception ex, string folder);
}
