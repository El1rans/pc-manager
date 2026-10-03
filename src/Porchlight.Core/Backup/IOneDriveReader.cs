namespace Porchlight.Core.Backup;

/// <summary>Reads OneDrive's sign-in state and Known Folder Move coverage. Read-only.</summary>
public interface IOneDriveReader
{
    /// <summary>Reads the registry; call off the UI thread. May throw an I/O, security or registry
    /// exception, which <see cref="IBackupStatusService"/> handles.</summary>
    OneDriveStatus Read();
}
