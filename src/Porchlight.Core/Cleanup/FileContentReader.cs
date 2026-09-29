namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IFileContentReader"/>
public sealed class FileContentReader : IFileContentReader
{
    private const int StreamBufferBytes = 64 * 1024;

    public Stream OpenRead(string path) =>
        new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            StreamBufferBytes, FileOptions.SequentialScan);
}
