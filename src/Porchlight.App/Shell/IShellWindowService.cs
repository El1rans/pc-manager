namespace Porchlight.App.Shell;

/// <summary>What the tray icon, alerts and single-instance activation need from the main window:
/// showing it, navigating it, and quitting the app properly. Safe to call from any thread.</summary>
public interface IShellWindowService
{
    /// <summary>Shows the main window (un-hiding it and restoring it from minimized) and brings it
    /// to the front.</summary>
    void ShowMainWindow();

    /// <summary>Selects the category and tab of the page whose view model is
    /// <paramref name="pageViewModelType"/>, showing the main window first. Does nothing if no such page exists.</summary>
    void NavigateTo(Type pageViewModelType);

    /// <summary>Quits Porchlight through the normal shutdown path, after asking for confirmation
    /// if a page reports work in flight (<see cref="IBusyGuard"/>).</summary>
    void RequestExit();
}
