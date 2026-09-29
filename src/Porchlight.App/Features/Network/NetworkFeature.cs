using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Network;

namespace Porchlight.App.Features.Network;

public static class NetworkFeature
{
    public static IServiceCollection AddNetworkFeature(this IServiceCollection services)
    {
        services.AddNetworkCore();
        // TryAdd: shared with the Get help and Lighting features; whichever registers first wins.
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        return services.AddPage<NetworkViewModel, NetworkView>();
    }
}
