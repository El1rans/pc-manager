using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.WindowsServices;

/// <summary>Registers the Core-side services behind the "Services" page (milestone 29).</summary>
public static class WindowsServicesServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsServicesCore(this IServiceCollection services)
    {
        services.AddSingleton<Changes.IChangeUndoer, ServiceStartTypeUndoer>();
        services.AddSingleton<Changes.IChangeUndoer, ServiceStateUndoer>();

#if DEBUG
        // DEBUG-only fake list for documentation screenshots; see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IWindowsServicesService, Demo.DemoWindowsServicesService>();
            return services;
        }
#endif

        // Also registered by the Startup feature; TryAdd keeps one instance whichever runs first.
        services.TryAddSingleton<Startup.IFileProductInfoReader, Startup.FileProductInfoReader>();
        services.AddSingleton<IServiceInfoSource, WmiServiceInfoSource>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<IWindowsServicesService, WindowsServicesService>();
        return services;
    }
}
