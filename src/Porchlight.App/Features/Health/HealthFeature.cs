using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

public static class HealthFeature
{
    public static IServiceCollection AddHealthFeature(this IServiceCollection services)
    {
        services.AddHealthCore();
        services.AddSingleton<DiskHealthCardViewModel>();
        services.AddSingleton<RepairCardViewModel>();
        services.AddSingleton<RestorePointCardViewModel>();
        services.AddSingleton<ProblemsCardViewModel>();
        services.AddSingleton<BatteryCardViewModel>();
        return services.AddPage<HealthViewModel, HealthView>();
    }
}
