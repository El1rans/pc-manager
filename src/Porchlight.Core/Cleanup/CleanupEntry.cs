namespace Porchlight.Core.Cleanup;

/// <summary>One file or directory as reported by <see cref="ICleanupFileSystem"/>.</summary>
/// <param name="FullPath">Full path of the entry.</param>
/// <param name="Name">File or directory name.</param>
/// <param name="Length">Size in bytes (0 for a directory).</param>
/// <param name="LastWriteUtc">Last modification time.</param>
/// <param name="Attributes">File attributes, including reparse-point and cloud-placeholder bits.</param>
public sealed record CleanupEntry(
    string FullPath,
    string Name,
    long Length,
    DateTime LastWriteUtc,
    FileAttributes Attributes)
{
    public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;

    /// <summary>Symlink, junction, mount point or other reparse point - never followed or deleted.</summary>
    public bool IsReparsePoint => (Attributes & FileAttributes.ReparsePoint) != 0;
}
