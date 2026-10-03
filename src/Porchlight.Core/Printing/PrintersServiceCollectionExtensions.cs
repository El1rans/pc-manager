using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Porchlight.Core.Printing;

/// <summary>Registers the Core-side services behind the "Printers" page (milestone 36).</summary>
public static class PrintersServiceCollectionExtensions
{
    public static IServiceCollection AddPrintersCore(this IServiceCollection services)
    {
        // Needs no demo fake: it only calls whichever IPrinterActions is registered.
        services.AddSingleton<PrinterFixFlow>();

#if DEBUG
        // DEBUG-only fake printers for documentation screenshots; see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<Demo.DemoPrinters>();
            services.AddSingleton<IPrinterService>(sp => sp.GetRequiredService<Demo.DemoPrinters>());
            services.AddSingleton<IPrinterActions>(sp => sp.GetRequiredService<Demo.DemoPrinters>());
            return services;
        }
#endif

        // Shared with the Windows services feature; TryAdd keeps one instance whichever runs first.
        services.TryAddSingleton<WindowsServices.IServiceManager, WindowsServices.ServiceManager>();
        services.AddSingleton<IPrinterSource, WmiPrinterSource>();
        services.AddSingleton<IPrinterService, PrinterService>();
        services.AddSingleton<IPrinterActions, PrinterActions>();
        return services;
    }
}
