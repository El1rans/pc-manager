using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Lighting;

namespace Porchlight.App.Features.Lighting;

public static class LightingFeature
{
    public static IServiceCollection AddLightingFeature(this IServiceCollection services) =>
        services.AddLightingCore().AddPage<LightingViewModel, LightingView>();
}
