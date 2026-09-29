using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Cleanup;

/// <summary>Registers the "Free up space" services. Like the other Core registrations it relies on
/// the host having called <c>AddComponents()</c> (for <c>IProcessRunner</c> and
/// <c>IProcessProbe</c>) and registered <c>ISettingsStore</c>.</summary>
public static class CleanupServiceCollectionExtensions
{
    public static IServiceCollection AddCleanupCore(this IServiceCollection services)
    {
        // Drive free/total for the page header: also registered by the Dashboard, so TryAdd.
        services.TryAddSingleton<IDriveMonitor, DriveMonitor>();
        services.TryAddSingleton<IElevationService, ElevationService>();
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<ICleanupPathProvider, CleanupPathProvider>();
        services.AddSingleton<ICleanupFileSystem, CleanupFileSystem>();
        services.AddSingleton<ICleanupCatalog, CleanupCatalog>();
#if DEBUG
        // Non-shipping (DEBUG-only) demo mode, same gate as the Dashboard's: made-up data and no
        // real deletes, for documentation screenshots. Absent from Release builds.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<ICleanupScanner, Demo.DemoCleanupScanner>();
            services.AddSingleton<ICleanupRunner, Demo.DemoCleanupRunner>();
            services.AddSingleton<ILargeFileFinder, Demo.DemoLargeFileFinder>();
            services.AddSingleton<IInstalledAppsReader, Demo.DemoInstalledAppsReader>();
            services.AddSingleton<IRecycler, Demo.DemoRecycler>();
            services.AddSingleton<IAppUninstaller, Demo.DemoAppUninstaller>();
            return services;
        }
#endif
        services.AddSingleton<IRecycleBin, RecycleBin>();
        services.AddSingleton<ICleanupScanner, CleanupScanner>();
        services.AddSingleton<ICleanupRunner, CleanupRunner>();
        services.AddSingleton<ILargeFileFinder, LargeFileFinder>();
        services.AddSingleton<IRecycler, Recycler>();
        services.AddSingleton<IInstalledAppsReader, InstalledAppsReader>();
        services.AddSingleton<IAppUninstaller, AppUninstaller>();
        return services;
    }
}
