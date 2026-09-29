using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <summary>Registers the Core services behind the Internet page (spec 18).</summary>
public static class NetworkServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkCore(this IServiceCollection services)
    {
#if DEBUG
        // Non-shipping (DEBUG-only) demo mode for screenshots - see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<INetworkStatusProvider, Demo.DemoNetworkStatusProvider>();
            services.AddSingleton<INetworkProbe, Demo.DemoNetworkProbe>();
            services.AddSingleton<INetworkTroubleshooter, NetworkTroubleshooter>();
            services.AddSingleton<INetworkRemedyService, Demo.DemoNetworkRemedyService>();
            services.AddSingleton<ISpeedTestService, Demo.DemoSpeedTestService>();
            services.AddSingleton<INetworkAppUsageService, Demo.DemoNetworkAppUsageService>();
            return services;
        }
#endif
        services.AddSingleton<IWifiInfoReader, WifiInfoReader>();
        services.AddSingleton<INetworkStatusProvider, NetworkStatusProvider>();
        services.AddSingleton<INetworkProbe>(sp => new NetworkProbe(
            sp.GetRequiredService<INetworkStatusProvider>(),
            new SocketsHttpHandler { AllowAutoRedirect = false },
            sp.GetRequiredService<ILogger<NetworkProbe>>()));
        services.AddSingleton<INetworkTroubleshooter, NetworkTroubleshooter>();
        services.AddSingleton<INetworkRemedyService, NetworkRemedyService>();
        services.AddSingleton<ISpeedTestService>(sp => new SpeedTestService(
            new SocketsHttpHandler(), TimeProvider.System, sp.GetRequiredService<ILogger<SpeedTestService>>()));
        services.AddSingleton<INetworkConnectionReader, NativeNetworkConnectionReader>();
        services.AddSingleton<IProcessNameResolver, ProcessNameResolver>();
        services.AddSingleton<INetworkAppUsageService, NetworkAppUsageService>();
        return services;
    }
}
