using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="ICleanupScanner"/>
public sealed partial class CleanupScanner : ICleanupScanner
{
    private readonly ICleanupFileSystem _fileSystem;
    private readonly IRecycleBin _recycleBin;
    private readonly IProcessProbe _processProbe;
    private readonly IElevationService _elevationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CleanupScanner> _logger;

    public CleanupScanner(
        ICleanupFileSystem fileSystem,
        IRecycleBin recycleBin,
        IProcessProbe processProbe,
        IElevationService elevationService,
        ILogger<CleanupScanner> logger)
        : this(fileSystem, recycleBin, processProbe, elevationService, TimeProvider.System, logger)
    {
    }

    internal CleanupScanner(
        ICleanupFileSystem fileSystem,
        IRecycleBin recycleBin,
        IProcessProbe processProbe,
        IElevationService elevationService,
        TimeProvider timeProvider,
        ILogger<CleanupScanner> logger)
    {
        _fileSystem = fileSystem;
        _recycleBin = recycleBin;
        _processProbe = processProbe;
        _elevationService = elevationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<CleanupScanResult> ScanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var isElevated = _elevationService.IsElevated;
        return Task.Run(() => Scan(categories, progress, isElevated, cancellationToken), cancellationToken);
    }

    private CleanupScanResult Scan(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        bool isElevated,
        CancellationToken cancellationToken)
    {
        var throttle = new ProgressThrottle(_timeProvider, ProgressThrottle.DefaultInterval);
        var results = new List<CleanupCategoryScan>(categories.Count);
        foreach (var category in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(ScanCategory(category, progress, throttle, isElevated, cancellationToken));
        }

        return new CleanupScanResult(results);
    }

    private CleanupCategoryScan ScanCategory(
        CleanupCategory category,
        IProgress<CleanupProgress>? progress,
        ProgressThrottle throttle,
        bool isElevated,
        CancellationToken cancellationToken)
    {
        if (category.RequiresAdmin && !isElevated)
        {
            return new CleanupCategoryScan(category.Id, 0, 0, []);
        }

        if (category.IsRecycleBin)
        {
            var info = _recycleBin.QuerySize();
            return new CleanupCategoryScan(category.Id, info.Bytes, (int)Math.Min(info.ItemCount, int.MaxValue), []);
        }

        var roots = CleanupRules.SelectRoots(category, isElevated, _processProbe, out var blocked);
        var cutoff = CleanupRules.CutoffUtc(category, _timeProvider);
        long bytes = 0;
        var files = 0;

        foreach (var root in roots)
        {
            if (!_fileSystem.DirectoryExists(root.Path))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(root.Path);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();

                IReadOnlyList<CleanupEntry> entries;
                try
                {
                    entries = _fileSystem.EnumerateEntries(directory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A folder we cannot read (in use, protected) contributes nothing to the size.
                    LogListFailed(ex, directory);
                    continue;
                }

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Safety rule 1: a reparse point is neither entered nor counted.
                    if (entry.IsReparsePoint || !CleanupPaths.IsInside(root.Path, entry.FullPath))
                    {
                        continue;
                    }

                    if (entry.IsDirectory)
                    {
                        if (category.FileNamePattern is null)
                        {
                            pending.Push(entry.FullPath);
                        }

                        continue;
                    }

                    if (!CleanupRules.MatchesPattern(category, entry.Name) || !CleanupRules.IsOldEnough(entry, cutoff))
                    {
                        continue;
                    }

                    bytes += entry.Length;
                    files++;
                    if (progress is not null && throttle.ShouldReport())
                    {
                        progress.Report(new CleanupProgress(category.Id, bytes, files));
                    }
                }
            }
        }

        progress?.Report(new CleanupProgress(category.Id, bytes, files));
        return new CleanupCategoryScan(category.Id, bytes, files, blocked);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read folder {Directory} while scanning; it is left out of the size.")]
    private partial void LogListFailed(Exception ex, string directory);
}
