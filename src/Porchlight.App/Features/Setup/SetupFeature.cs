using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.App.Features.Setup;

/// <summary>
/// Registers the first-run setup dialog. Unlike a normal feature, this one adds no
/// <c>IPage</c> to the navigation rail - it is a modal dialog, shown once automatically and
/// afterwards on demand (see <see cref="ISetupLauncher"/>).
/// </summary>
public static class SetupFeature
{
    public static IServiceCollection AddSetupFeature(this IServiceCollection services)
    {
        services.AddSingleton<ISetupLauncher, SetupLauncher>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<SetupWindow>();
        return services;
    }
}
