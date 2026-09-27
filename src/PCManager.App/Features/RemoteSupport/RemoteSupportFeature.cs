using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;
using PCManager.Core.RemoteSupport;

namespace PCManager.App.Features.RemoteSupport;

public static class RemoteSupportFeature
{
    public static IServiceCollection AddRemoteSupportFeature(this IServiceCollection services)
    {
        services.AddRemoteSupportCore();
        services.AddSingleton<IClipboardService, ClipboardService>();
        return services.AddPage<RemoteSupportViewModel, RemoteSupportView>();
    }
}
