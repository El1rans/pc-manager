using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

public static class DashboardFeature
{
    public static IServiceCollection AddDashboardFeature(this IServiceCollection services) =>
        services.AddMonitoring().AddPage<DashboardViewModel, DashboardView>();
}
