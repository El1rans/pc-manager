using Microsoft.Extensions.DependencyInjection;

namespace PCManager.Core.Monitoring;

/// <summary>Registers the live system monitoring services used by the Dashboard feature.</summary>
public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoring(this IServiceCollection services)
    {
#if DEBUG
        // Non-shipping (DEBUG-only) escape hatch for capturing documentation screenshots without
        // exposing the real machine's computer name, hardware, drives or running processes - see
        // Monitoring.Demo.DemoDataMode and CONTRIBUTING.md's "Screenshots" section. Has no effect,
        // and the branch below does not exist at all, in a Release build.
        if (Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<ISystemInfoProvider, Demo.DemoSystemInfoProvider>();
            services.AddSingleton<IPerformanceSampler, PerformanceSampler>();
            services.AddSingleton<IProcessMonitor, Demo.DemoProcessMonitor>();
            services.AddSingleton<IDriveMonitor, Demo.DemoDriveMonitor>();
            services.AddSingleton<IRestartDetector, RestartDetector>();
            return services;
        }
#endif
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IPerformanceSampler, PerformanceSampler>();
        services.AddSingleton<IProcessMonitor, ProcessMonitor>();
        services.AddSingleton<IDriveMonitor, DriveMonitor>();
        services.AddSingleton<IRestartDetector, RestartDetector>();
        return services;
    }
}
