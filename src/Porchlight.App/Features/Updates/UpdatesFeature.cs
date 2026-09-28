using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Processes;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

public static class UpdatesFeature
{
    public static IServiceCollection AddUpdatesFeature(this IServiceCollection services)
    {
        services.AddWingetClient();
        services.AddAppInUseDiagnostics();
        services.AddPage<UpdatesViewModel, UpdatesView>();
        services.AddHostedService<UpdatesAutoCheckHostedService>();
        return services;
    }
}
