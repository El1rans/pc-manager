using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.Backup;

/// <summary>Registers the backup status services. The real readers are registered unconditionally
/// (they are used for the check-up report in demo mode too); only the status service is faked.</summary>
public static class BackupServiceCollectionExtensions
{
    public static IServiceCollection AddBackupCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFileHistoryReader, FileHistoryReader>();
        services.AddSingleton<IOneDriveReader, OneDriveReader>();
        services.AddSingleton<IBackupToolDetector, BackupToolDetector>();
#if DEBUG
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IBackupStatusService, Demo.DemoBackupStatusService>();
            return services;
        }
#endif
        services.AddSingleton<IBackupStatusService, BackupStatusService>();
        return services;
    }
}
