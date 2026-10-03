using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.RemoveApps;

namespace Porchlight.App.Features.RemoveApps;

public static class RemoveAppsFeature
{
    public static IServiceCollection AddRemoveAppsFeature(this IServiceCollection services)
    {
        services.AddRemoveAppsCore();
        // TryAdd: shared with the Cleanup and Services features, which register the same dialog.
        services.TryAddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddPage<RemoveAppsViewModel, RemoveAppsView>();
    }
}
