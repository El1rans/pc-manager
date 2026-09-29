using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Lighting;

namespace Porchlight.App.Features.Lighting;

public static class LightingFeature
{
    public static IServiceCollection AddLightingFeature(this IServiceCollection services)
    {
        services.AddLightingCore();
        // The conflict-warning panel's "Open Dynamic Lighting settings" button uses the same
        // generic OS-URL-launcher the RemoteSupport feature already has - see its own registration
        // for why this is TryAdd, not AddSingleton.
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        services.TryAddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAnimationFilePicker, AnimationFilePicker>();
        return services.AddPage<LightingViewModel, LightingView>();
    }
}
