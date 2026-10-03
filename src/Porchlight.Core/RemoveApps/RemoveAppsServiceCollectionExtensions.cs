using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;
using Porchlight.Core.Winget;

namespace Porchlight.Core.RemoveApps;

/// <summary>Registers the services behind the "Remove apps" page (milestone 35). The reader and
/// uninstaller are shared with the Cleanup feature, so they are added with TryAdd.</summary>
public static class RemoveAppsServiceCollectionExtensions
{
    public static IServiceCollection AddRemoveAppsCore(this IServiceCollection services)
    {
        services.AddWingetClient();
        services.TryAddSingleton<IElevationService, ElevationService>();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
#if DEBUG
        // DEBUG-only fakes (nothing is ever removed); see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IRemoveAppsService, Demo.DemoRemoveAppsService>();
            return services;
        }
#endif
        services.TryAddSingleton<IInstalledAppsReader, InstalledAppsReader>();
        services.TryAddSingleton<IAppUninstaller, AppUninstaller>();
        services.AddSingleton<IRemoveAppsService, RemoveAppsService>();
        return services;
    }
}
