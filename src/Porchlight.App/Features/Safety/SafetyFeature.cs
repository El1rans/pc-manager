using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Safety;

namespace Porchlight.App.Features.Safety;

public static class SafetyFeature
{
    public static IServiceCollection AddSafetyFeature(this IServiceCollection services)
    {
        services.AddSafetyCore();
        // TryAdd: shared with the Network, Cleanup and Lighting features (opens ms-settings: links too).
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        return services.AddPage<SafetyViewModel, SafetyView>();
    }
}
