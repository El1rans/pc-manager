using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.Core.Startup;

namespace Porchlight.App.Features.Startup;

public static class StartupFeature
{
    public static IServiceCollection AddStartupFeature(this IServiceCollection services)
    {
        services.AddStartupCore();
        return services.AddPage<StartupViewModel, StartupView>();
    }
}
