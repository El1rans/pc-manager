using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

public static class DashboardFeature
{
    public static IServiceCollection AddDashboardFeature(this IServiceCollection services) =>
        services.AddMonitoring().AddPage<DashboardViewModel, DashboardView>();
}
