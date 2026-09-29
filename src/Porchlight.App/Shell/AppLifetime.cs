namespace Porchlight.App.Shell;

/// <inheritdoc cref="IAppLifetime"/>
public sealed class AppLifetime : IAppLifetime
{
    public void Shutdown(int exitCode = 0)
    {
        // Before Shutdown closes the windows, so the main window closes for real instead of
        // hiding itself to the tray.
        AppExitState.MarkExiting();
        System.Windows.Application.Current?.Shutdown(exitCode);
    }
}
