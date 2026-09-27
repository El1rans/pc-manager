using Microsoft.Extensions.DependencyInjection;

namespace PCManager.Core.Winget;

/// <summary>Registers <see cref="IWingetClient"/>. Relies on <c>IProcessRunner</c> already being
/// registered - see <c>PCManager.Core.Components.ComponentServiceCollectionExtensions.AddComponents</c>,
/// which every host that adds the Updates feature is expected to call.</summary>
public static class WingetServiceCollectionExtensions
{
    public static IServiceCollection AddWingetClient(this IServiceCollection services)
    {
        services.AddSingleton<IWingetClient, WingetClient>();
        return services;
    }
}
