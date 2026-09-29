namespace Porchlight.Core.Cleanup;

/// <summary>Resolves the well-known folders the cleanup catalog and finders need, so tests can point
/// everything at a temp directory. Any value may be empty if it could not be resolved; the catalog
/// then leaves the affected categories out rather than guessing.</summary>
public interface ICleanupPathProvider
{
    string TempPath { get; }

    string WindowsDirectory { get; }

    string LocalAppData { get; }

    string ProgramData { get; }

    string UserProfile { get; }

    string ProgramFiles { get; }

    string ProgramFilesX86 { get; }

    string DownloadsFolder { get; }

    /// <summary>Desktop, Documents, Downloads, Videos, Pictures and Music (may contain duplicates or
    /// nested folders, e.g. when OneDrive redirects Documents; the finder de-duplicates).</summary>
    IReadOnlyList<string> PersonalFolders { get; }
}
