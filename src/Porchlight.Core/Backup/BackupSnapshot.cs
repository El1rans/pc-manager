namespace Porchlight.Core.Backup;

/// <summary>Everything the backup sources said. A null source could not be read (it is logged by the
/// service); that is different from "not set up".</summary>
/// <param name="FileHistory">File History, or null when it could not be read.</param>
/// <param name="OneDrive">OneDrive, or null when it could not be read.</param>
/// <param name="OtherTools">Friendly names of well-known backup tools that are installed.</param>
public sealed record BackupSnapshot(
    FileHistoryStatus? FileHistory, OneDriveStatus? OneDrive, IReadOnlyList<string> OtherTools);
