namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="ICleanupFileSystem"/>
public sealed class CleanupFileSystem : ICleanupFileSystem
{
    // Hidden and system entries are included on purpose (temp folders are full of them); reparse
    // points are listed too so callers can recognise and skip them.
    private static readonly EnumerationOptions ListOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<CleanupEntry> EnumerateEntries(string directory)
    {
        var entries = new List<CleanupEntry>();
        foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", ListOptions))
        {
            var length = info is FileInfo file ? file.Length : 0;
            entries.Add(new CleanupEntry(info.FullName, info.Name, length, info.LastWriteTimeUtc, info.Attributes));
        }

        return entries;
    }

    public void DeleteFile(string path)
    {
        // Re-checked right before deleting: a reparse point is never touched, even if one appeared
        // after the directory was listed.
        RefuseReparsePoint(path);
        File.Delete(path);
    }

    public void DeleteEmptyDirectory(string path)
    {
        RefuseReparsePoint(path);
        Directory.Delete(path, recursive: false);
    }

    private static void RefuseReparsePoint(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Refusing to delete a reparse point.");
        }
    }
}
