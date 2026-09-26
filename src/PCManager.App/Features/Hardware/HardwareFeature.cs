using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;

namespace PCManager.App.Features.Hardware;

public static class HardwareFeature
{
    public static IServiceCollection AddHardwareFeature(this IServiceCollection services)
    {
        services.AddSingleton<HardwareViewModel>();
        services.AddSingleton<IPage>(sp => sp.GetRequiredService<HardwareViewModel>());
        return services;
    }
}
