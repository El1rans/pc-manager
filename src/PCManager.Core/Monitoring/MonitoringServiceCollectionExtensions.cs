using Microsoft.Extensions.DependencyInjection;

namespace PCManager.Core.Monitoring;

/// <summary>Registers the live system monitoring services used by the Dashboard feature.</summary>
public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        services.AddSingleton<IPerformanceSampler, PerformanceSampler>();
        services.AddSingleton<IProcessMonitor, ProcessMonitor>();
        services.AddSingleton<IDriveMonitor, DriveMonitor>();
        services.AddSingleton<IRestartDetector, RestartDetector>();
        return services;
    }
}
