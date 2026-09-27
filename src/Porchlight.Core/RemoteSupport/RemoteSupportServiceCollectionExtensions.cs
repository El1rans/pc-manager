using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.RemoteSupport;

/// <summary>Registers the Core-side services behind the "Get help" page (milestone 06). Detection,
/// install and start of AnyDesk itself go through <c>IComponentService</c> (registered by
/// <c>AddComponents</c>); this only adds the ID/alias reading on top.</summary>
public static class RemoteSupportServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteSupportCore(this IServiceCollection services)
    {
        services.AddSingleton<IAnyDeskConfigReader, AnyDeskConfigReader>();

#if DEBUG
        // Non-shipping (DEBUG-only) escape hatch for capturing "Get help" documentation
        // screenshots without exposing the real machine's AnyDesk address - see
        // Monitoring.Demo.DemoDataMode and CONTRIBUTING.md's "Screenshots" section. Has no effect,
        // and the branch below does not exist at all, in a Release build.
        if (Monitoring.Demo.DemoDataMode.IsEnabled)
        {
            services.AddSingleton<IAnyDeskService, Demo.DemoAnyDeskService>();
            return services;
        }
#endif

        services.AddSingleton<IAnyDeskService, AnyDeskService>();
        return services;
    }
}
