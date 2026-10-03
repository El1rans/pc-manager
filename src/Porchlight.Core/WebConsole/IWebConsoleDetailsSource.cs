namespace Porchlight.Core.WebConsole;

/// <summary>Supplies the extra views of the web console (updates waiting, startup impact, security
/// status). Read-only by design: implementations only observe the PC and never change anything on it.
/// Implementations must be cheap to call repeatedly (cache or reuse last-known results) because an
/// open browser page asks for them every 30 seconds.</summary>
public interface IWebConsoleDetailsSource
{
    /// <summary>App updates waiting at the last check made in Porchlight; never starts a new check.</summary>
    Task<WebConsoleUpdates> GetUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>Startup items with their impact.</summary>
    Task<WebConsoleStartup> GetStartupAsync(CancellationToken cancellationToken);

    /// <summary>The safety picture: security, Windows Update and remote-control programs.</summary>
    Task<WebConsoleSecurity> GetSecurityAsync(CancellationToken cancellationToken);
}
