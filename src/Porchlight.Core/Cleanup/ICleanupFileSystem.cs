namespace Porchlight.Core.Cleanup;

/// <summary>The only disk access the cleanup scanner, runner and file finder use, so the safety
/// rules can be unit-tested against an in-memory fake.</summary>
public interface ICleanupFileSystem
{
    bool DirectoryExists(string path);

    /// <summary>Lists the direct children of <paramref name="directory"/> (files and directories,
    /// including hidden and system ones). Never follows a reparse point. Throws
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> if the directory can't
    /// be read.</summary>
    IReadOnlyList<CleanupEntry> EnumerateEntries(string directory);

    /// <summary>Deletes one file. Throws <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> if it is in use or protected - never forces it.</summary>
    void DeleteFile(string path);

    /// <summary>Deletes a directory only if it is empty. Throws <see cref="IOException"/> otherwise.</summary>
    void DeleteEmptyDirectory(string path);
}
