using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.Core.Processes;

/// <summary>Registers <see cref="IAppInUseDiagnosticsService"/> and its
/// <see cref="IAppLockDetector"/> dependency. Relies on <c>Components.IRegistryReader</c> already
/// being registered - see <c>Components.ComponentServiceCollectionExtensions.AddComponents</c>,
/// which every host that adds the Updates feature already calls (same rule as
/// <c>Winget.WingetServiceCollectionExtensions.AddWingetClient</c>'s <c>IProcessRunner</c> note).</summary>
public static class AppInUseDiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddAppInUseDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<IAppLockDetector, RestartManagerLockDetector>();
        services.AddSingleton<IAppInUseDiagnosticsService, AppInUseDiagnosticsService>();
        return services;
    }
}
