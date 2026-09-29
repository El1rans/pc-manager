using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Health;

/// <summary>Registers the System health services.</summary>
public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddHealthCore(this IServiceCollection services)
    {
        services.AddSingleton<IWindowsRepairService, WindowsRepairService>();
#if DEBUG
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IDiskHealthService, Demo.DemoDiskHealthService>();
            services.AddSingleton<IRestorePointService, Demo.DemoRestorePointService>();
            services.AddSingleton<IProblemEventReader, Demo.DemoProblemEventReader>();
            services.AddSingleton<IBatteryService, Demo.DemoBatteryService>();
            return services;
        }
#endif
        services.AddSingleton<IDiskHealthService, WmiDiskHealthService>();
        services.AddSingleton<IRestorePointService, RestorePointService>();
        services.AddSingleton<IProblemEventReader, EventLogProblemReader>();
        services.AddSingleton<IBatteryService, WmiBatteryService>();
        return services;
    }
}
