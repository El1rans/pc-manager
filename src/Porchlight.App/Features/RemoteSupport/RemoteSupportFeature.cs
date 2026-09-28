using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.RemoteSupport;

namespace Porchlight.App.Features.RemoteSupport;

public static class RemoteSupportFeature
{
    public static IServiceCollection AddRemoteSupportFeature(this IServiceCollection services)
    {
        services.AddRemoteSupportCore();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<IWindowsVersionReader, WindowsVersionReader>();
        return services.AddPage<RemoteSupportViewModel, RemoteSupportView>();
    }
}
