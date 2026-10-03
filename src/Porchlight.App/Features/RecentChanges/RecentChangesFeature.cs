using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Changes;

namespace Porchlight.App.Features.RecentChanges;

public static class RecentChangesFeature
{
    public static IServiceCollection AddRecentChangesFeature(this IServiceCollection services)
    {
        services.AddChangesCore();
        return services.AddPage<RecentChangesViewModel, RecentChangesView>();
    }
}
