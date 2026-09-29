namespace Porchlight.Core.Cleanup;

/// <summary>One folder on the disk space map, with the size of everything below it.</summary>
public sealed class DiskNode
{
    private readonly List<DiskNode> _children = [];
    private readonly List<DiskFile> _files = [];

    internal DiskNode(string path, string name, DiskNode? parent)
    {
        Path = path;
        Name = name;
        Parent = parent;
    }

    public string Path { get; }

    public string Name { get; }

    public DiskNode? Parent { get; }

    /// <summary>Bytes in all files below this folder (cloud-only placeholders count as 0).</summary>
    public long TotalBytes { get; internal set; }

    /// <summary>Number of files below this folder.</summary>
    public long FileCount { get; internal set; }

    /// <summary>How many folders in this subtree (this one included) could not be read.</summary>
    public int UnreadableFolders { get; internal set; }

    /// <summary>Child folders, largest first.</summary>
    public IReadOnlyList<DiskNode> Children => _children;

    /// <summary>The largest files directly in this folder (at most
    /// <see cref="DiskSpaceMapper.MaxFilesPerFolder"/>), largest first.</summary>
    public IReadOnlyList<DiskFile> Files => _files;

    internal List<DiskNode> ChildList => _children;

    internal List<DiskFile> FileList => _files;

    /// <summary>Removes a listed file (after it was moved to the Recycle Bin) and reduces this
    /// folder's and every ancestor's totals, so the map stays right without a rescan.</summary>
    public bool RemoveFile(DiskFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!_files.Remove(file))
        {
            return false;
        }

        for (var node = this; node is not null; node = node.Parent)
        {
            node.TotalBytes = Math.Max(0, node.TotalBytes - file.Bytes);
            node.FileCount = Math.Max(0, node.FileCount - 1);
        }

        return true;
    }
}
