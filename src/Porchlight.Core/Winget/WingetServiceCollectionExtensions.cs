using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Winget;

/// <summary>Registers <see cref="IWingetClient"/>. Relies on <c>IProcessRunner</c> already being
/// registered - see <c>Porchlight.Core.Components.ComponentServiceCollectionExtensions.AddComponents</c>,
/// which every host that adds the Updates feature is expected to call.</summary>
public static class WingetServiceCollectionExtensions
{
    public static IServiceCollection AddWingetClient(this IServiceCollection services)
    {
        services.AddSingleton<IPendingUpdatesTracker, PendingUpdatesTracker>();

#if DEBUG
        // Non-shipping (DEBUG-only) escape hatch for capturing Updates page documentation
        // screenshots without exposing the real machine's installed apps - see
        // Monitoring.Demo.DemoDataMode and CONTRIBUTING.md's "Screenshots" section. Never starts a
        // real winget process. Has no effect, and the branch below does not exist at all, in a
        // Release build.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IWingetClient, Demo.DemoWingetClient>();
            services.AddSingleton<IUpdateHistoryStore, Demo.FakeUpdateHistoryStore>();
            return services;
        }
#endif

        services.AddSingleton<IWingetClient, WingetClient>();
        services.AddSingleton<IUpdateHistoryStore, UpdateHistoryStore>();
        return services;
    }
}
