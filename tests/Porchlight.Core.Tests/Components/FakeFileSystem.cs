using Porchlight.Core.Components;

namespace Porchlight.Core.Tests.Components;

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _existingFiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ProgramFilesDirectories { get; init; } =
        [@"C:\Program Files", @"C:\Program Files (x86)"];

    public void AddFile(string path) => _existingFiles.Add(path);

    public bool FileExists(string path) => _existingFiles.Contains(path);

    public bool DirectoryExists(string path) => false;
}
