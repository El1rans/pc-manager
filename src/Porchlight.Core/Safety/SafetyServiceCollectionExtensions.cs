using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.Safety;

/// <summary>Registers the Core-side services behind the "Safety" page (milestone 31).</summary>
public static class SafetyServiceCollectionExtensions
{
    public static IServiceCollection AddSafetyCore(this IServiceCollection services)
    {
        services.AddSingleton<ISafetyStatusService, SafetyStatusService>();
#if DEBUG
        // DEBUG-only fakes for documentation screenshots; see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<ISecurityStatusService, Demo.DemoSecurityStatusService>();
            services.AddSingleton<IWindowsUpdateStatusService, Demo.DemoWindowsUpdateStatusService>();
            services.AddSingleton<IRemoteAccessService, Demo.DemoRemoteAccessService>();
            return services;
        }
#endif

        // Shared with the Cleanup and Running apps features; TryAdd keeps one registration each.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<Cleanup.IInstalledAppsReader, Cleanup.InstalledAppsReader>();
        services.TryAddSingleton<RunningApps.IProcessSnapshotSource, RunningApps.ProcessSnapshotSource>();

        services.AddSingleton<ISecurityCenterReader, SecurityCenterReader>();
        services.AddSingleton<IWindowsFirewallReader, WindowsFirewallReader>();
        services.AddSingleton<ISecurityStatusService, SecurityStatusService>();
        services.AddSingleton<IWindowsUpdateAgent, WindowsUpdateAgent>();
        services.AddSingleton<IWindowsUpdateStatusService, WindowsUpdateStatusService>();
        services.AddSingleton<IRemoteToolProbe, RemoteToolProbe>();
        services.AddSingleton<IRemoteAccessService, RemoteAccessService>();
        return services;
    }
}
