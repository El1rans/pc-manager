using Porchlight.Core.Health;

namespace Porchlight.Core.Backup.Demo;

/// <summary>DEBUG demo data: File History last ran 12 days ago and OneDrive only protects Pictures.</summary>
internal sealed class DemoBackupStatusService : IBackupStatusService
{
    private const int DemoDaysSinceBackup = 12;

    public Task<HealthReadResult<BackupSnapshot>> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            new FileHistoryStatus(true, true, DateTimeOffset.Now.AddDays(-DemoDaysSinceBackup)),
            new OneDriveStatus(true, true, false, false, true, null),
            ["Dropbox"])));
}
