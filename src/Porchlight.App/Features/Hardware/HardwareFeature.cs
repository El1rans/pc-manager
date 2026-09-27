using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

public static class HardwareFeature
{
    public static IServiceCollection AddHardwareFeature(this IServiceCollection services)
    {
        services.AddHardwareCore();
        services.AddHostedService<HardwareHostedService>();
        return services.AddPage<HardwareViewModel, HardwareView>();
    }
}
