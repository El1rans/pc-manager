using System.IO;
using Porchlight.Core.Cleanup;

namespace Porchlight.App.Tests.Features.Cleanup;

/// <summary>A tiny in-memory <see cref="ICleanupFileSystem"/> for driving the real
/// <see cref="DiskSpaceMapper"/> from view model tests (a DiskNode tree can only be built by the mapper).</summary>
internal sealed class TreeFileSystem : ICleanupFileSystem
{
    private static readonly DateTime Modified = new(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Dictionary<string, List<CleanupEntry>> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unreadable = new(StringComparer.OrdinalIgnoreCase);

    public TreeFileSystem Dir(string path)
    {
        Ensure(path);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Ensure(parent).Add(new CleanupEntry(path, Path.GetFileName(path), 0, Modified, FileAttributes.Directory));
        }

        return this;
    }

    public TreeFileSystem File(string path, long bytes)
    {
        Ensure(Path.GetDirectoryName(path)!).Add(
            new CleanupEntry(path, Path.GetFileName(path), bytes, Modified, FileAttributes.Normal));
        return this;
    }

    public TreeFileSystem Unreadable(string path)
    {
        _unreadable.Add(path);
        return this;
    }

    public bool DirectoryExists(string path) => _dirs.ContainsKey(path);

    public IReadOnlyList<CleanupEntry> EnumerateEntries(string directory)
    {
        if (_unreadable.Contains(directory))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return _dirs.TryGetValue(directory, out var entries) ? entries : [];
    }

    public void DeleteFile(string path) => throw new NotSupportedException();

    public void DeleteEmptyDirectory(string path) => throw new NotSupportedException();

    private List<CleanupEntry> Ensure(string path)
    {
        if (!_dirs.TryGetValue(path, out var list))
        {
            _dirs[path] = list = [];
        }

        return list;
    }
}
