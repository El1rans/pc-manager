namespace Porchlight.App.Shell;

/// <inheritdoc cref="IAppLifetime"/>
public sealed class AppLifetime : IAppLifetime
{
    public void Shutdown(int exitCode = 0) => System.Windows.Application.Current?.Shutdown(exitCode);
}
