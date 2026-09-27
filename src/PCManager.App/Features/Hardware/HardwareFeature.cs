using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;
using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

public static class HardwareFeature
{
    public static IServiceCollection AddHardwareFeature(this IServiceCollection services)
    {
        services.AddHardwareCore();
        services.AddHostedService<HardwareHostedService>();
        return services.AddPage<HardwareViewModel, HardwareView>();
    }
}
