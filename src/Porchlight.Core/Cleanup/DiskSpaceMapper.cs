using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IDiskSpaceMapper"/>
public sealed partial class DiskSpaceMapper : IDiskSpaceMapper
{
    /// <summary>How many of the largest files are kept per folder.</summary>
    public const int MaxFilesPerFolder = 25;

    private readonly ICleanupFileSystem _fileSystem;
    private readonly ICleanupPathProvider _paths;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DiskSpaceMapper> _logger;

    public DiskSpaceMapper(ICleanupFileSystem fileSystem, ICleanupPathProvider paths, ILogger<DiskSpaceMapper> logger)
        : this(fileSystem, paths, TimeProvider.System, logger)
    {
    }

    internal DiskSpaceMapper(
        ICleanupFileSystem fileSystem, ICleanupPathProvider paths, TimeProvider timeProvider, ILogger<DiskSpaceMapper> logger)
    {
        _fileSystem = fileSystem;
        _paths = paths;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<DiskNode> MapAsync(string root, IProgress<DiskMapProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return Task.Run(() => Map(root, progress, cancellationToken), cancellationToken);
    }

    private DiskNode Map(string root, IProgress<DiskMapProgress>? progress, CancellationToken cancellationToken)
    {
        if (!_fileSystem.DirectoryExists(root))
        {
            throw new DirectoryNotFoundException("The folder does not exist.");
        }

        var personalFolders = _paths.PersonalFolders.Where(folder => !string.IsNullOrWhiteSpace(folder)).ToList();
        var throttle = new ProgressThrottle(_timeProvider, ProgressThrottle.DefaultInterval);
        var rootNode = new DiskNode(root, NameOf(root), null);

        // Discovery order: a parent always comes before its children, so walking this list backwards
        // aggregates every child into its parent before the parent is itself aggregated.
        var discovered = new List<DiskNode> { rootNode };
        var pending = new Stack<DiskNode>();
        pending.Push(rootNode);
        long bytes = 0;
        long files = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = pending.Pop();

            IReadOnlyList<CleanupEntry> entries;
            try
            {
                entries = _fileSystem.EnumerateEntries(node.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Expected for protected folders; they are counted and reported as "couldn't read".
                LogListFailed(ex, node.Path);
                node.UnreadableFolders = 1;
                continue;
            }

            var listed = new List<DiskFile>();
            foreach (var entry in entries)
            {
                if (entry.IsReparsePoint)
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    var child = new DiskNode(entry.FullPath, entry.Name, node);
                    node.ChildList.Add(child);
                    discovered.Add(child);
                    pending.Push(child);
                    continue;
                }

                var length = PersonalFileRules.IsCloudPlaceholder(entry) ? 0 : entry.Length;
                node.TotalBytes += length;
                node.FileCount++;
                bytes += length;
                files++;
                if (length > 0)
                {
                    listed.Add(new DiskFile(
                        entry.FullPath, entry.Name, length, entry.LastWriteUtc, CanRecycle(entry, personalFolders)));
                }
            }

            listed.Sort(static (a, b) => b.Bytes.CompareTo(a.Bytes));
            node.FileList.AddRange(listed.Take(MaxFilesPerFolder));

            if (progress is not null && throttle.ShouldReport())
            {
                progress.Report(new DiskMapProgress(node.Path, bytes, files));
            }
        }

        for (var i = discovered.Count - 1; i >= 0; i--)
        {
            var node = discovered[i];
            node.ChildList.Sort(static (a, b) => b.TotalBytes.CompareTo(a.TotalBytes));
            if (node.Parent is { } parent)
            {
                parent.TotalBytes += node.TotalBytes;
                parent.FileCount += node.FileCount;
                parent.UnreadableFolders += node.UnreadableFolders;
            }
        }

        progress?.Report(new DiskMapProgress(rootNode.Path, bytes, files));
        return rootNode;
    }

    private static bool CanRecycle(CleanupEntry entry, List<string> personalFolders)
    {
        if ((entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
        {
            return false;
        }

        foreach (var folder in personalFolders)
        {
            if (CleanupPaths.IsInside(folder, entry.FullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static string NameOf(string root)
    {
        var name = Path.GetFileName(root.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? root : name;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read folder {Directory} while mapping disk space.")]
    private partial void LogListFailed(Exception ex, string directory);
}
