using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;

namespace PCManager.App.Features.Dashboard;

public static class DashboardFeature
{
    public static IServiceCollection AddDashboardFeature(this IServiceCollection services)
    {
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<IPage>(sp => sp.GetRequiredService<DashboardViewModel>());
        return services;
    }
}
