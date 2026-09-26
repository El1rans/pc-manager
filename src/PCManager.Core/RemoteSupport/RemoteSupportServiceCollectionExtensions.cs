using Microsoft.Extensions.DependencyInjection;

namespace PCManager.Core.RemoteSupport;

/// <summary>Registers the Core-side services behind the "Get help" page (milestone 06). Detection,
/// install and start of AnyDesk itself go through <c>IComponentService</c> (registered by
/// <c>AddComponents</c>); this only adds the ID/alias reading on top.</summary>
public static class RemoteSupportServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteSupportCore(this IServiceCollection services)
    {
        services.AddSingleton<IAnyDeskConfigReader, AnyDeskConfigReader>();
        services.AddSingleton<IAnyDeskService, AnyDeskService>();
        return services;
    }
}
