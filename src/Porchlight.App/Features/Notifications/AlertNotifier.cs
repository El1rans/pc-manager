using Porchlight.App.Features.Dashboard;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Features.Updates;
using Porchlight.App.Shell;
using Porchlight.App.Tray;
using Porchlight.Core.Alerts;

namespace Porchlight.App.Features.Notifications;

/// <summary>Shows an <see cref="Alert"/> as a tray balloon whose click opens the alert's page.</summary>
public sealed class AlertNotifier(ITrayIcon trayIcon, IShellWindowService shell)
{
    public void Show(Alert alert)
    {
        var page = PageFor(alert.Target);
        trayIcon.ShowBalloon(alert.Title, alert.Message, () => shell.NavigateTo(page));
    }

    internal static Type PageFor(AlertTarget target) => target switch
    {
        AlertTarget.Updates => typeof(UpdatesViewModel),
        AlertTarget.Hardware => typeof(HardwareViewModel),
        _ => typeof(DashboardViewModel),
    };
}
