using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;
using PCManager.Core.Lighting;

namespace PCManager.App.Features.Lighting;

public static class LightingFeature
{
    public static IServiceCollection AddLightingFeature(this IServiceCollection services) =>
        services.AddLightingCore().AddPage<LightingViewModel, LightingView>();
}
