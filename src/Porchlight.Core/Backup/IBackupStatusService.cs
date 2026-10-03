using Porchlight.Core.Health;

namespace Porchlight.Core.Backup;

/// <summary>Reads every backup source and tolerates any one of them failing.</summary>
public interface IBackupStatusService
{
    /// <summary>Fails only when neither File History nor OneDrive could be read.</summary>
    Task<HealthReadResult<BackupSnapshot>> GetAsync(CancellationToken cancellationToken);
}
