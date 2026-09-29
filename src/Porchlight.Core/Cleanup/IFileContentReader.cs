namespace Porchlight.Core.Cleanup;

/// <summary>The only way the duplicate finder reads file contents, so tests can use in-memory
/// files and prove how much of each file was read.</summary>
public interface IFileContentReader
{
    /// <summary>Opens <paramref name="path"/> for sequential reading (seekable, never writing).
    /// Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> if the file is in
    /// use or protected.</summary>
    Stream OpenRead(string path);
}
