using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.RunningApps;

namespace Porchlight.App.Features.RunningApps;

public static class RunningAppsFeature
{
    public static IServiceCollection AddRunningAppsFeature(this IServiceCollection services)
    {
        services.AddRunningAppsCore();
        // TryAdd: shared with the Cleanup feature (and any other page that asks before ending something).
        services.TryAddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddPage<RunningAppsViewModel, RunningAppsView>();
    }
}
