using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.Browsers;

/// <summary>Registers the Core-side services behind the "Browser add-ons" page (milestone 17).
/// Relies on <c>IProcessRunner</c> already being registered (see
/// <c>Winget.WingetServiceCollectionExtensions.AddWingetClient</c>).</summary>
public static class BrowsersServiceCollectionExtensions
{
    public static IServiceCollection AddBrowsersCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IBrowserLocations, BrowserLocations>();
        services.AddSingleton<IBrowserExecutableLocator, BrowserExecutableLocator>();
        services.AddSingleton<IBrowserAddOnsOpener, BrowserAddOnsOpener>();
        services.AddSingleton<IBrowserPolicyReader, BrowserPolicyReader>();

#if DEBUG
        // Non-shipping (DEBUG-only) fake add-on list for documentation screenshots - see
        // Monitoring.Demo.DemoDataMode. Never reads the real machine's browser profiles.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IBrowserExtensionScanner, Demo.DemoBrowserExtensionScanner>();
            services.AddSingleton<IBrowserHijackScanner, Demo.DemoBrowserHijackScanner>();
            return services;
        }
#endif

        services.AddSingleton<IBrowserExtensionScanner, BrowserExtensionScanner>();
        services.AddSingleton<IBrowserHijackScanner, BrowserHijackScanner>();
        return services;
    }
}
