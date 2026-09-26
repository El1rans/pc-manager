namespace PCManager.Core.Components;

/// <summary>
/// The narrow slice of file-system access <c>ComponentService</c> needs for detection, behind an
/// interface so detection is unit-testable without touching the real disk.
/// </summary>
public interface IFileSystem
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Whether a directory exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary><c>%ProgramFiles%</c> and <c>%ProgramFiles(x86)%</c>, in that order.</summary>
    IReadOnlyList<string> ProgramFilesDirectories { get; }
}
