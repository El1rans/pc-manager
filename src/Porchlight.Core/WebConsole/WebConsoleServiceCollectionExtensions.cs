using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Settings;

namespace Porchlight.Core.WebConsole;

/// <summary>Registers the Core-side web console services. Needs <c>AddMonitoring()</c> and
/// <c>AddHardwareCore()</c> (for the shared system info, drive, restart and hardware sources) and an
/// <see cref="ISettingsStore"/>.</summary>
public static class WebConsoleServiceCollectionExtensions
{
    public static IServiceCollection AddWebConsoleCore(this IServiceCollection services)
    {
        // Built by hand rather than resolved: the collector must own a PerformanceSampler and
        // ProcessMonitor of its own, not the dashboard's shared singletons - see
        // WebConsoleStatsCollector.
        services.AddSingleton<IWebConsoleStatsSource>(sp => new WebConsoleStatsCollector(
            sp.GetRequiredService<ISystemInfoProvider>(),
            new PerformanceSampler(sp.GetRequiredService<ILogger<PerformanceSampler>>()),
#if DEBUG
            // Same DEBUG-only demo data as the dashboard - see Monitoring.Demo.DemoDataMode.
            Monitoring.Demo.DemoDataMode.IsEnabled
                ? new Monitoring.Demo.DemoProcessMonitor()
                :
#endif
                new ProcessMonitor(sp.GetRequiredService<ILogger<ProcessMonitor>>()),
            sp.GetRequiredService<IDriveMonitor>(),
            sp.GetRequiredService<IRestartDetector>(),
            sp.GetRequiredService<IHardwareService>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<ILogger<WebConsoleStatsCollector>>()));
        services.AddSingleton<WebConsoleRouter>();
        services.AddSingleton<IWebConsoleServer, WebConsoleServer>();
        services.AddSingleton<ILocalAddressProvider, LocalAddressProvider>();
        services.AddSingleton<IPortAvailability, PortAvailability>();
        services.AddSingleton<WebConsoleController>();
        return services;
    }
}
