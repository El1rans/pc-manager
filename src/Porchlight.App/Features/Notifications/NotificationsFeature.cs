using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;
using Porchlight.App.Tray;
using Porchlight.Core.Alerts;

namespace Porchlight.App.Features.Notifications;

/// <summary>Registers the tray icon, background alerts, scheduled update checks and the
/// Notifications dialog. Adds no page to the navigation rail. See
/// <c>docs/specs/15-tray-and-alerts.md</c>.</summary>
public static class NotificationsFeature
{
    public static IServiceCollection AddNotificationsFeature(this IServiceCollection services)
    {
        services.AddAlertsCore();

        services.AddSingleton<IShellWindowService, ShellWindowService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ITrayIcon, TrayIcon>();
        services.AddSingleton<TrayService>();

        services.AddSingleton<IUpdateChecker, UpdateChecker>();
        services.AddSingleton<AlertNotifier>();
        services.AddHostedService<AlertHostedService>();
        services.AddHostedService<ScheduledUpdateCheckHostedService>();
        services.AddHostedService<LoginLaunchRefreshHostedService>();

        services.AddSingleton<INotificationsLauncher, NotificationsLauncher>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<NotificationsWindow>();
        return services;
    }
}
