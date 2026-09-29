using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.App.Features.Notifications;

/// <inheritdoc cref="INotificationsLauncher"/>
public sealed class NotificationsLauncher(IServiceProvider serviceProvider) : INotificationsLauncher
{
    private NotificationsWindow? _open;

    public void Show()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(Show);
            return;
        }

        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var window = serviceProvider.GetRequiredService<NotificationsWindow>();
        // Owned by the main window only while it is visible: an owner that is hidden in the tray
        // would hide this dialog along with it.
        if (Application.Current?.MainWindow is { IsVisible: true } main)
        {
            window.Owner = main;
            window.ShowInTaskbar = false;
        }

        window.Closed += (_, _) => _open = null;
        _open = window;
        window.Show();
        window.Activate();
    }
}
