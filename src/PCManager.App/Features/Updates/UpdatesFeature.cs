using Microsoft.Extensions.DependencyInjection;
using PCManager.App.Shell;
using PCManager.Core.Winget;

namespace PCManager.App.Features.Updates;

public static class UpdatesFeature
{
    public static IServiceCollection AddUpdatesFeature(this IServiceCollection services)
    {
        services.AddWingetClient();
        services.AddPage<UpdatesViewModel, UpdatesView>();
        services.AddHostedService<UpdatesAutoCheckHostedService>();
        return services;
    }
}
