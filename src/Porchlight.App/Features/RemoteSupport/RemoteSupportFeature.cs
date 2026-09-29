using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Shell;
using Porchlight.Core.Checkup;
using Porchlight.Core.RemoteSupport;

namespace Porchlight.App.Features.RemoteSupport;

public static class RemoteSupportFeature
{
    public static IServiceCollection AddRemoteSupportFeature(this IServiceCollection services)
    {
        services.AddRemoteSupportCore();
        // TryAdd: the Lighting feature (its conflict-warning panel's "Open Dynamic Lighting
        // settings" button, and its custom animations' "Copy AI prompt"/"Paste from clipboard")
        // also depends on IUrlLauncher and IClipboardService and registers them the same way -
        // whichever feature's AddXFeature() runs first wins, and the other's registration is a
        // no-op instead of creating a second, redundant singleton instance.
        services.TryAddSingleton<IClipboardService, ClipboardService>();
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<IWindowsVersionReader, WindowsVersionReader>();
        services.AddCheckup();
        services.TryAddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<CheckupCardViewModel>();
        return services.AddPage<RemoteSupportViewModel, RemoteSupportView>();
    }
}
