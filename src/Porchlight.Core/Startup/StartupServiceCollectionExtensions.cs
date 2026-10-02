using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Startup;

/// <summary>Registers the Core-side services behind the "Startup apps" page (milestone 13).</summary>
public static class StartupServiceCollectionExtensions
{
    public static IServiceCollection AddStartupCore(this IServiceCollection services)
    {
        // The "start when I sign in" task (spec 24) is real even in demo mode: the demo only fakes the list.
        services.AddSingleton<Elevation.IElevatedCommandRunner, Elevation.ElevatedCommandRunner>();
        services.AddSingleton<ILoginLaunchEnvironment, LoginLaunchEnvironment>();
        services.AddSingleton<ILoginLaunchService, LoginLaunchService>();

#if DEBUG
        // DEBUG-only fake list for documentation screenshots; see Monitoring.Demo.DemoDataMode.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IStartupInfoReader, StartupInfoReader>();
        services.AddSingleton<IStartupService, Demo.DemoStartupService>();
            return services;
        }
#endif

        services.AddSingleton<IStartupRegistry, StartupRegistry>();
        services.AddSingleton<IStartupFolderReader, StartupFolderReader>();
        services.AddSingleton<IFileProductInfoReader, FileProductInfoReader>();
        services.AddSingleton<IStartupService, StartupService>();
        return services;
    }
}
