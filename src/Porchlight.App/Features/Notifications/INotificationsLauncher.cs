namespace Porchlight.App.Features.Notifications;

/// <summary>Shows the small "Notifications" settings dialog (from the tray menu and the sidebar
/// footer).</summary>
public interface INotificationsLauncher
{
    /// <summary>Shows the dialog, or brings it forward if it is already open. Safe from any thread.</summary>
    void Show();
}
