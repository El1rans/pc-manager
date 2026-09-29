namespace Porchlight.App.Shell;

/// <summary>Process-wide "the app is really exiting" flag. Set by <see cref="AppLifetime"/> (and by
/// the session-ending handler) so <see cref="MainWindow"/>'s close-to-tray logic lets the window
/// close instead of hiding it - covering tray Exit, the elevated relaunch, and Windows sign-out.</summary>
public static class AppExitState
{
    private static volatile bool _isExiting;

    public static bool IsExiting => _isExiting;

    public static void MarkExiting() => _isExiting = true;
}
