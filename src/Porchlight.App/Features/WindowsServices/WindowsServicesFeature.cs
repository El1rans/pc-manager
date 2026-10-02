using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.WindowsServices;

namespace Porchlight.App.Features.WindowsServices;

public static class WindowsServicesFeature
{
    public static IServiceCollection AddWindowsServicesFeature(this IServiceCollection services)
    {
        services.AddWindowsServicesCore();
        // TryAdd: shared with the Cleanup feature, which registers the same dialog.
        services.TryAddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        return services.AddPage<WindowsServicesViewModel, WindowsServicesView>();
    }
}
