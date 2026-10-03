using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Printing;

namespace Porchlight.App.Features.Printers;

public static class PrintersFeature
{
    public static IServiceCollection AddPrintersFeature(this IServiceCollection services)
    {
        services.AddPrintersCore();
        // TryAdd: shared with the Cleanup, Services and Remote support features.
        services.TryAddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        return services.AddPage<PrintersViewModel, PrintersView>();
    }
}
