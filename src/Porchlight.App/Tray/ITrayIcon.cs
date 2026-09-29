namespace Porchlight.App.Tray;

/// <summary>The notification-area icon. Every member is safe to call from any thread (calls are
/// marshalled to the UI thread that owns the icon's window). See
/// <c>docs/specs/15-tray-and-alerts.md</c>.</summary>
public interface ITrayIcon : IDisposable
{
    /// <summary>True once the icon has been added to the notification area and not removed. Used to
    /// decide whether closing the window may hide it to the tray - a hidden window with no icon
    /// would leave the user no way back.</summary>
    bool IsVisible { get; }

    /// <summary>Raised (on the UI thread) when the icon is double-clicked.</summary>
    event EventHandler? DoubleClicked;

    /// <summary>Adds the icon with the given right-click menu.</summary>
    void Show(IReadOnlyList<TrayMenuItem> menu);

    /// <summary>Sets the hover tooltip (truncated to Windows' limit).</summary>
    void SetTooltip(string text);

    /// <summary>Shows a balloon (a toast on Windows 10/11). <paramref name="onClick"/> runs on the
    /// UI thread if the user clicks it; only the most recent balloon's action is kept.</summary>
    void ShowBalloon(string title, string message, Action? onClick);
}
