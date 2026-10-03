using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Backup;

/// <inheritdoc cref="IBackupToolDetector"/>
public sealed class BackupToolDetector(IInstalledAppsReader installedApps) : IBackupToolDetector
{
    public IReadOnlyList<string> Find() =>
        BackupToolMatcher.Find(installedApps.GetInstalledApps().Select(app => app.DisplayName));
}
