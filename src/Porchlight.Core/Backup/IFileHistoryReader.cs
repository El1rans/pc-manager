namespace Porchlight.Core.Backup;

/// <summary>Reads File History's configuration and last backup time. Read-only.</summary>
public interface IFileHistoryReader
{
    /// <summary>Reads the config files and registry; call off the UI thread. May throw an I/O,
    /// security or registry exception, which <see cref="IBackupStatusService"/> handles.</summary>
    FileHistoryStatus Read();
}
