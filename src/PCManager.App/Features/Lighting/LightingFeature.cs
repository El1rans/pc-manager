using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;

namespace PCManager.App.Features.Lighting;

public static class LightingFeature
{
    public static IServiceCollection AddLightingFeature(this IServiceCollection services)
    {
        services.AddSingleton<LightingViewModel>();
        services.AddSingleton<IPage>(sp => sp.GetRequiredService<LightingViewModel>());
        return services;
    }
}
