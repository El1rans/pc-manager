using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IDuplicateFinder"/>
/// <remarks>Groups by size, then by a hash of the first and last 64 KB, then by a full streamed
/// SHA-256, so file contents are read only for files that could still be duplicates. Skips reparse
/// points, hidden/system files and cloud-only placeholders.</remarks>
public sealed partial class DuplicateFinder : IDuplicateFinder
{
    /// <summary>How much of the start and of the end of a file the quick check reads.</summary>
    public const int PartialBytes = 64 * 1024;

    private const int FullHashBufferBytes = 1024 * 1024;

    private readonly ICleanupFileSystem _fileSystem;
    private readonly IFileContentReader _reader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DuplicateFinder> _logger;

    public DuplicateFinder(ICleanupFileSystem fileSystem, IFileContentReader reader, ILogger<DuplicateFinder> logger)
        : this(fileSystem, reader, TimeProvider.System, logger)
    {
    }

    internal DuplicateFinder(
        ICleanupFileSystem fileSystem, IFileContentReader reader, TimeProvider timeProvider, ILogger<DuplicateFinder> logger)
    {
        _fileSystem = fileSystem;
        _reader = reader;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<DuplicateGroup>> FindAsync(
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run<IReadOnlyList<DuplicateGroup>>(() => Find(options, progress, cancellationToken), cancellationToken);
    }

    private sealed record Candidate(string FullPath, string Name, string Folder, long Length, DateTime LastWriteUtc);

    private List<DuplicateGroup> Find(
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken)
    {
        var throttle = new ProgressThrottle(_timeProvider, ProgressThrottle.DefaultInterval);

        // Stage 1: list files and group by exact size. No file content is read.
        var listed = List(options, progress, throttle, cancellationToken);
        var candidates = listed
            .GroupBy(file => file.Length)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToList();

        // Stage 2: the start and end of each same-size file.
        var quick = Regroup(
            candidates, DuplicateStage.QuickCheck, progress, throttle,
            (file, _) => PartialHash(file), cancellationToken);

        // Stage 3: a full hash, only for files that still match after the quick check.
        var buffer = new byte[FullHashBufferBytes];
        var confirmed = Regroup(
            quick.SelectMany(group => group).ToList(), DuplicateStage.Confirming, progress, throttle,
            (file, token) => FullHash(file, buffer, token), cancellationToken);

        return confirmed
            .Select(group => new DuplicateGroup(
                group[0].Length,
                group
                    .OrderByDescending(file => file.LastWriteUtc)
                    .ThenBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
                    .Select(file => new DuplicateFile(file.FullPath, file.Name, file.Folder, file.LastWriteUtc))
                    .ToList()))
            .OrderByDescending(group => group.WastedBytes)
            .ThenBy(group => group.Files[0].FullPath, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxGroups)
            .ToList();
    }

    private List<Candidate> List(
        DuplicateSearchOptions options,
        IProgress<DuplicateProgress>? progress,
        ProgressThrottle throttle,
        CancellationToken cancellationToken)
    {
        var found = new List<Candidate>();
        foreach (var root in LargeFileFinder.DistinctRoots(options.Roots))
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
                    }
                    else if (entry.Length >= options.MinimumBytes)
                    {
                        found.Add(new Candidate(entry.FullPath, entry.Name, directory, entry.Length, entry.LastWriteUtc));
                    }
                }

                if (progress is not null && throttle.ShouldReport())
                {
                    progress.Report(new DuplicateProgress(DuplicateStage.Listing, found.Count, 0));
                }
            }
        }

        return found;
    }

    /// <summary>Hashes every file with <paramref name="hash"/> and keeps only groups of two or more
    /// files that share size and hash. Files that cannot be read are dropped.</summary>
    private List<List<Candidate>> Regroup(
        List<Candidate> files,
        DuplicateStage stage,
        IProgress<DuplicateProgress>? progress,
        ProgressThrottle throttle,
        Func<Candidate, CancellationToken, string> hash,
        CancellationToken cancellationToken)
    {
        var groups = new Dictionary<(long Length, string Hash), List<Candidate>>();
        var done = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key;
            try
            {
                key = hash(file, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogReadFailed(ex, file.FullPath);
                done++;
                continue;
            }

            if (!groups.TryGetValue((file.Length, key), out var list))
            {
                groups[(file.Length, key)] = list = [];
            }

            list.Add(file);
            done++;
            if (progress is not null && throttle.ShouldReport())
            {
                progress.Report(new DuplicateProgress(stage, done, files.Count));
            }
        }

        return groups.Values.Where(list => list.Count > 1).ToList();
    }

    private string PartialHash(Candidate file)
    {
        using var stream = _reader.OpenRead(file.FullPath);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[PartialBytes];

        if (file.Length <= 2L * PartialBytes)
        {
            Feed(stream, hasher, buffer, file.Length);
        }
        else
        {
            Feed(stream, hasher, buffer, PartialBytes);
            stream.Seek(file.Length - PartialBytes, SeekOrigin.Begin);
            Feed(stream, hasher, buffer, PartialBytes);
        }

        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    private static void Feed(Stream stream, IncrementalHash hasher, byte[] buffer, long count)
    {
        var remaining = count;
        while (remaining > 0)
        {
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                return;
            }

            hasher.AppendData(buffer, 0, read);
            remaining -= read;
        }
    }

    private string FullHash(Candidate file, byte[] buffer, CancellationToken cancellationToken)
    {
        using var stream = _reader.OpenRead(file.FullPath);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hasher.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read folder {Directory} while looking for duplicates.")]
    private partial void LogListFailed(Exception ex, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read {Path} while looking for duplicates; it is left out.")]
    private partial void LogReadFailed(Exception ex, string path);
}
