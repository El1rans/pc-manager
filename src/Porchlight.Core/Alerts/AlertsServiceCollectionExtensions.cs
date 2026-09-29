using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Tray;

namespace Porchlight.Core.Alerts;

/// <summary>Registers the Core services behind the tray icon and background alerts. The monitoring
/// and hardware services it depends on are registered by their own features.</summary>
public static class AlertsServiceCollectionExtensions
{
    public static IServiceCollection AddAlertsCore(this IServiceCollection services)
    {
        // TimeProvider.System is passed explicitly (rather than registering it globally) so this
        // feature does not change how any other service resolves its own constructors.
        services.AddSingleton<IAlertStateStore, SettingsAlertStateStore>();
        services.AddSingleton<IAlertInputProvider>(sp => new AlertInputProvider(
            sp.GetRequiredService<IDriveMonitor>(),
            sp.GetRequiredService<IHardwareService>(),
            sp.GetRequiredService<IRestartDetector>(),
            TimeProvider.System,
            sp.GetRequiredService<ILogger<AlertInputProvider>>()));
        services.AddSingleton<IQuickStatsProvider, QuickStatsProvider>();
        services.AddSingleton(sp => new AlertEvaluator(TimeProvider.System, sp.GetRequiredService<IAlertStateStore>()));
        return services;
    }
}
