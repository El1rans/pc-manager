using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="ICleanupRunner"/>
/// <remarks>Implements the safety rules of docs/specs/12-disk-cleanup.md: reparse points are never
/// followed or deleted, every path is re-checked to be inside its category root, files younger than
/// the category's minimum age are left alone, a failed delete is counted and never retried or
/// forced, and directories are removed only when empty, bottom-up, never the root itself.</remarks>
public sealed partial class CleanupRunner : ICleanupRunner
{
    private readonly ICleanupFileSystem _fileSystem;
    private readonly IRecycleBin _recycleBin;
    private readonly IProcessProbe _processProbe;
    private readonly IElevationService _elevationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupRunner> _logger;

    public CleanupRunner(
        ICleanupFileSystem fileSystem,
        IRecycleBin recycleBin,
        IProcessProbe processProbe,
        IElevationService elevationService,
        ILogger<CleanupRunner> logger)
        : this(fileSystem, recycleBin, processProbe, elevationService, TimeProvider.System, logger)
    {
    }

    internal CleanupRunner(
        ICleanupFileSystem fileSystem,
        IRecycleBin recycleBin,
        IProcessProbe processProbe,
        IElevationService elevationService,
        TimeProvider timeProvider,
        ILogger<CleanupRunner> logger)
    {
        _fileSystem = fileSystem;
        _recycleBin = recycleBin;
        _processProbe = processProbe;
        _elevationService = elevationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<CleanupRunResult> CleanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var isElevated = _elevationService.IsElevated;

        // The token is deliberately not passed to Task.Run: cancelling must still return the partial
        // result, which the work itself produces once it notices the token between files.
        return Task.Run(() => Clean(categories, progress, isElevated, cancellationToken), CancellationToken.None);
    }

    private CleanupRunResult Clean(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        bool isElevated,
        CancellationToken cancellationToken)
    {
        var totals = new RunTotals();
        var blockedPrograms = new List<string>();
        var throttle = new ProgressThrottle(_timeProvider, ProgressThrottle.DefaultInterval);
        var cancelled = false;

        try
        {
            foreach (var category in categories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (category.RequiresAdmin && !isElevated)
                {
                    continue;
                }

                if (category.IsRecycleBin)
                {
                    EmptyRecycleBin(category, totals, progress);
                    continue;
                }

                CleanCategory(category, isElevated, totals, blockedPrograms, progress, throttle, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        return new CleanupRunResult(totals.BytesFreed, totals.FilesDeleted, totals.FilesSkipped, cancelled, blockedPrograms);
    }

    private void EmptyRecycleBin(CleanupCategory category, RunTotals totals, IProgress<CleanupProgress>? progress)
    {
        var info = _recycleBin.QuerySize();
        if (info.ItemCount == 0 && info.Bytes == 0)
        {
            return;
        }

        if (_recycleBin.Empty())
        {
            var items = (int)Math.Min(info.ItemCount, int.MaxValue);
            totals.BytesFreed += info.Bytes;
            totals.FilesDeleted += items;
            progress?.Report(new CleanupProgress(category.Id, info.Bytes, items));
        }
    }

    private void CleanCategory(
        CleanupCategory category,
        bool isElevated,
        RunTotals totals,
        List<string> blockedPrograms,
        IProgress<CleanupProgress>? progress,
        ProgressThrottle throttle,
        CancellationToken cancellationToken)
    {
        var roots = CleanupRules.SelectRoots(category, isElevated, _processProbe, out var blocked);
        foreach (var name in blocked.Where(name => !blockedPrograms.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            blockedPrograms.Add(name);
        }

        var cutoff = CleanupRules.CutoffUtc(category, _timeProvider);
        foreach (var root in roots)
        {
            if (!_fileSystem.DirectoryExists(root.Path))
            {
                continue;
            }

            CleanRoot(category, root, cutoff, totals, () =>
            {
                if (progress is not null && throttle.ShouldReport())
                {
                    progress.Report(new CleanupProgress(category.Id, totals.BytesFreed, totals.FilesDeleted));
                }
            }, cancellationToken);
        }

        progress?.Report(new CleanupProgress(category.Id, totals.BytesFreed, totals.FilesDeleted));
    }

    private void CleanRoot(
        CleanupCategory category,
        CleanupRoot root,
        DateTime cutoff,
        RunTotals totals,
        Action onFileDeleted,
        CancellationToken cancellationToken)
    {
        var directories = new List<DirectoryNode> { new(root.Path, -1, DateTime.MinValue) };
        var pending = new Stack<int>();
        pending.Push(0);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = pending.Pop();
            var node = directories[index];

            IReadOnlyList<CleanupEntry> entries;
            try
            {
                entries = _fileSystem.EnumerateEntries(node.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable folder: leave it (and therefore its parents) in place.
                LogListFailed(ex, node.Path);
                node.HasRemainingContent = true;
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Safety rule 1: reparse points are never followed, counted or deleted.
                if (entry.IsReparsePoint)
                {
                    node.HasRemainingContent = true;
                    continue;
                }

                // Safety rule 2: re-check containment after full normalisation.
                if (!CleanupPaths.IsInside(root.Path, entry.FullPath))
                {
                    LogOutsideRoot(entry.FullPath, root.Path);
                    node.HasRemainingContent = true;
                    if (!entry.IsDirectory)
                    {
                        totals.FilesSkipped++;
                    }

                    continue;
                }

                if (entry.IsDirectory)
                {
                    if (category.FileNamePattern is null)
                    {
                        directories.Add(new DirectoryNode(entry.FullPath, index, entry.LastWriteUtc));
                        pending.Push(directories.Count - 1);
                    }
                    else
                    {
                        node.HasRemainingContent = true;
                    }

                    continue;
                }

                if (!CleanupRules.MatchesPattern(category, entry.Name) || !CleanupRules.IsOldEnough(entry, cutoff))
                {
                    node.HasRemainingContent = true;
                    continue;
                }

                if (TryDeleteFile(entry))
                {
                    totals.BytesFreed += entry.Length;
                    totals.FilesDeleted++;
                    onFileDeleted();
                }
                else
                {
                    totals.FilesSkipped++;
                    node.HasRemainingContent = true;
                }
            }
        }

        RemoveEmptyDirectories(directories, cutoff);
    }

    private bool TryDeleteFile(CleanupEntry entry)
    {
        try
        {
            _fileSystem.DeleteFile(entry.FullPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Safety rule 5: in use / access denied is normal. Never retried, forced or unlocked.
            LogDeleteFailed(ex, entry.FullPath);
            return false;
        }
    }

    /// <summary>Safety rule 6: bottom-up (children were added after their parents, so a reverse walk
    /// visits every child before its parent), only when empty, never the root (index 0), and never a
    /// directory younger than the category's minimum age.</summary>
    private void RemoveEmptyDirectories(List<DirectoryNode> directories, DateTime cutoff)
    {
        for (var i = directories.Count - 1; i >= 1; i--)
        {
            var node = directories[i];
            var parent = directories[node.ParentIndex];

            if (node.HasRemainingContent || node.LastWriteUtc > cutoff)
            {
                parent.HasRemainingContent = true;
                continue;
            }

            try
            {
                _fileSystem.DeleteEmptyDirectory(node.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not empty after all, or in use: leave it, and keep its parents too.
                LogDirectoryKept(ex, node.Path);
                parent.HasRemainingContent = true;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read folder {Directory} while cleaning; left in place.")]
    private partial void LogListFailed(Exception ex, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped {Path}: it is not inside the cleanup folder {Root}.")]
    private partial void LogOutsideRoot(string path, string root);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Left {Path} alone (in use or protected).")]
    private partial void LogDeleteFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Kept folder {Path} (not empty or in use).")]
    private partial void LogDirectoryKept(Exception ex, string path);

    private sealed class RunTotals
    {
        public long BytesFreed { get; set; }

        public int FilesDeleted { get; set; }

        public int FilesSkipped { get; set; }
    }

    private sealed class DirectoryNode
    {
        public DirectoryNode(string path, int parentIndex, DateTime lastWriteUtc)
        {
            Path = path;
            ParentIndex = parentIndex;
            LastWriteUtc = lastWriteUtc;
        }

        public string Path { get; }

        public int ParentIndex { get; }

        public DateTime LastWriteUtc { get; }

        public bool HasRemainingContent { get; set; }
    }
}
