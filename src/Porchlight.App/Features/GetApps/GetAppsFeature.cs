using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.GetApps;

public static class GetAppsFeature
{
    public static IServiceCollection AddGetAppsFeature(this IServiceCollection services)
    {
        services.AddWingetClient();
        return services.AddPage<GetAppsViewModel, GetAppsView>();
    }
}
