using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.RunningApps;

/// <summary>Registers the Core-side services behind the "Running apps" page (spec 28).</summary>
public static class RunningAppsServiceCollectionExtensions
{
    public static IServiceCollection AddRunningAppsCore(this IServiceCollection services)
    {
        // Shared with the Remote support, Cleanup and other features; TryAdd keeps one registration.
        services.TryAddSingleton<Processes.IProcessRunner, Processes.ProcessRunner>();

#if DEBUG
        // DEBUG-only fake list for documentation screenshots; see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IRunningAppsService, Demo.DemoRunningAppsService>();
            return services;
        }
#endif

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<Startup.IFileProductInfoReader, Startup.FileProductInfoReader>();
        services.AddSingleton<IProcessSnapshotSource, ProcessSnapshotSource>();
        services.AddSingleton<IProcessKiller, ProcessKiller>();
        services.AddSingleton<ISystemMemoryInfo, SystemMemoryInfo>();
        services.AddSingleton<IRunningAppsService, RunningAppsService>();
        return services;
    }
}
