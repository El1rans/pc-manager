using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>In-memory <see cref="ICleanupFileSystem"/>: a tree of files and directories keyed by
/// full path, with hooks for locked files, reparse points and injected (out-of-root) entries. Paths
/// are Windows paths that never touch the real disk.</summary>
internal sealed class FakeCleanupFileSystem : ICleanupFileSystem
{
    private readonly Dictionary<string, CleanupEntry> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CleanupEntry>> _injected = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _locked = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unreadable = new(StringComparer.OrdinalIgnoreCase);

    public List<string> DeletedFiles { get; } = [];

    public List<string> DeletedDirectories { get; } = [];

    /// <summary>Every DeleteFile call, successful or not, so tests can prove nothing is retried.</summary>
    public List<string> DeleteFileAttempts { get; } = [];

    /// <summary>Every directory that was listed, so tests can prove a reparse point was never entered.</summary>
    public List<string> ListedDirectories { get; } = [];

    /// <summary>Runs after each successful file delete (used to cancel mid-run).</summary>
    public Action<string>? AfterFileDeleted { get; set; }

    public FakeCleanupFileSystem AddDirectory(string path, DateTime? lastWriteUtc = null, FileAttributes extra = 0)
    {
        _nodes[Normalize(path)] = new CleanupEntry(
            path, Path.GetFileName(path), 0, lastWriteUtc ?? DateTime.MinValue, FileAttributes.Directory | extra);
        return this;
    }

    public FakeCleanupFileSystem AddFile(
        string path, long length, DateTime lastWriteUtc, FileAttributes attributes = FileAttributes.Normal)
    {
        _nodes[Normalize(path)] = new CleanupEntry(path, Path.GetFileName(path), length, lastWriteUtc, attributes);
        return this;
    }

    /// <summary>A directory that is a reparse point (junction/symlink), with content behind it.</summary>
    public FakeCleanupFileSystem AddReparseDirectory(string path, DateTime? lastWriteUtc = null) =>
        AddDirectory(path, lastWriteUtc, FileAttributes.ReparsePoint);

    public FakeCleanupFileSystem Lock(string path)
    {
        _locked.Add(Normalize(path));
        return this;
    }

    public FakeCleanupFileSystem MakeUnreadable(string directory)
    {
        _unreadable.Add(Normalize(directory));
        return this;
    }

    /// <summary>Makes a listing of <paramref name="directory"/> also return <paramref name="entry"/>,
    /// which does not really live there (e.g. a path outside the category root).</summary>
    public FakeCleanupFileSystem Inject(string directory, CleanupEntry entry)
    {
        var key = Normalize(directory);
        if (!_injected.TryGetValue(key, out var list))
        {
            _injected[key] = list = [];
        }

        list.Add(entry);
        return this;
    }

    public bool Exists(string path) => _nodes.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) =>
        _nodes.TryGetValue(Normalize(path), out var node) && node.IsDirectory;

    public IReadOnlyList<CleanupEntry> EnumerateEntries(string directory)
    {
        var key = Normalize(directory);
        ListedDirectories.Add(key);
        if (_unreadable.Contains(key))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var entries = _nodes
            .Where(pair => string.Equals(ParentOf(pair.Key), key, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .ToList();
        if (_injected.TryGetValue(key, out var extra))
        {
            entries.AddRange(extra);
        }

        return entries;
    }

    public void DeleteFile(string path)
    {
        var key = Normalize(path);
        DeleteFileAttempts.Add(key);
        if (_locked.Contains(key))
        {
            throw new IOException("The process cannot access the file because it is being used by another process.");
        }

        if (_nodes.Remove(key))
        {
            DeletedFiles.Add(key);
            AfterFileDeleted?.Invoke(key);
        }
    }

    public void DeleteEmptyDirectory(string path)
    {
        var key = Normalize(path);
        if (_nodes.Keys.Any(other => string.Equals(ParentOf(other), key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new IOException("The directory is not empty.");
        }

        _nodes.Remove(key);
        DeletedDirectories.Add(key);
    }

    private static string Normalize(string path) => path.TrimEnd('\\', '/');

    private static string ParentOf(string key) => Path.GetDirectoryName(key) ?? string.Empty;
}
