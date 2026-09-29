using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="ILargeFileFinder"/>
/// <remarks>Skips reparse points, hidden and system files/folders, and cloud-only placeholders
/// (their size would not free any local space).</remarks>
public sealed partial class LargeFileFinder : ILargeFileFinder
{
    private readonly ICleanupFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LargeFileFinder> _logger;

    public LargeFileFinder(ICleanupFileSystem fileSystem, ILogger<LargeFileFinder> logger)
        : this(fileSystem, TimeProvider.System, logger)
    {
    }

    internal LargeFileFinder(ICleanupFileSystem fileSystem, TimeProvider timeProvider, ILogger<LargeFileFinder> logger)
    {
        _fileSystem = fileSystem;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<FoundFile>> FindAsync(LargeFileSearchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run<IReadOnlyList<FoundFile>>(() => Find(options, cancellationToken), cancellationToken);
    }

    private List<FoundFile> Find(LargeFileSearchOptions options, CancellationToken cancellationToken)
    {
        var cutoff = options.MinimumAge is { } age ? _timeProvider.GetUtcNow().UtcDateTime - age : (DateTime?)null;
        var extensions = options.Extensions is null
            ? null
            : new HashSet<string>(options.Extensions, StringComparer.OrdinalIgnoreCase);

        // Min-heap on size: the smallest of the current best N is at the top and is evicted first.
        var best = new PriorityQueue<FoundFile, long>();

        foreach (var root in DistinctRoots(options.Roots))
        {
            if (!_fileSystem.DirectoryExists(root))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(root);
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
                    LogListFailed(ex, directory);
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (PersonalFileRules.IsSkipped(entry))
                    {
                        continue;
                    }

                    if (entry.IsDirectory)
                    {
                        pending.Push(entry.FullPath);
                        continue;
                    }

                    if (!Qualifies(entry, options, extensions, cutoff))
                    {
                        continue;
                    }

                    best.Enqueue(
                        new FoundFile(entry.FullPath, entry.Name, directory, entry.Length, entry.LastWriteUtc),
                        entry.Length);
                    if (best.Count > options.MaxResults)
                    {
                        best.Dequeue();
                    }
                }
            }
        }

        var results = new List<FoundFile>(best.Count);
        while (best.TryDequeue(out var file, out _))
        {
            results.Add(file);
        }

        results.Reverse();
        return results;
    }

    private static bool Qualifies(
        CleanupEntry entry, LargeFileSearchOptions options, HashSet<string>? extensions, DateTime? cutoff)
    {
        if (entry.Length < options.MinimumBytes)
        {
            return false;
        }

        if (cutoff is { } limit && entry.LastWriteUtc > limit)
        {
            return false;
        }

        return extensions is null || extensions.Contains(Path.GetExtension(entry.Name));
    }

    /// <summary>Normalises and de-duplicates roots, dropping any that sit inside another (for
    /// example a OneDrive-redirected Documents that is also under Desktop), so no file is listed twice.</summary>
    internal static List<string> DistinctRoots(IEnumerable<string> roots)
    {
        var normalised = new List<string>();
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            {
                continue;
            }

            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length > 0 && !normalised.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                normalised.Add(full);
            }
        }

        return normalised
            .Where(candidate => !normalised.Any(other =>
                !string.Equals(other, candidate, StringComparison.OrdinalIgnoreCase) &&
                CleanupPaths.IsInside(other, candidate)))
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read folder {Directory} while looking for large files.")]
    private partial void LogListFailed(Exception ex, string directory);
}
