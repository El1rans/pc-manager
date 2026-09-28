using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Shell;
using Porchlight.Core.RemoteSupport;

namespace Porchlight.App.Features.RemoteSupport;

public static class RemoteSupportFeature
{
    public static IServiceCollection AddRemoteSupportFeature(this IServiceCollection services)
    {
        services.AddRemoteSupportCore();
        services.AddSingleton<IClipboardService, ClipboardService>();
        // TryAdd: the Lighting feature (its conflict-warning panel's "Open Dynamic Lighting
        // settings" button) also depends on IUrlLauncher and registers it the same way - whichever
        // feature's AddXFeature() runs first wins, and the other's registration is a no-op instead
        // of creating a second, redundant singleton instance.
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<IWindowsVersionReader, WindowsVersionReader>();
        return services.AddPage<RemoteSupportViewModel, RemoteSupportView>();
    }
}
