using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Cleanup;

namespace Porchlight.App.Features.Cleanup;

public static class CleanupFeature
{
    public static IServiceCollection AddCleanupFeature(this IServiceCollection services)
    {
        services.AddCleanupCore();
        // TryAdd: shared with the Remote support and Lighting features (opens ms-settings: links too).
        services.TryAddSingleton<IUrlLauncher, UrlLauncher>();
        services.AddSingleton<IConfirmationDialog, MessageBoxConfirmationDialog>();
        return services.AddPage<CleanupViewModel, CleanupView>();
    }
}
