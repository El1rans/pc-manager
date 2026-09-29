namespace Porchlight.Core.WebConsole;

/// <summary>
/// The read-only web console's HTTP listener: serves a small stats page and <c>/api/stats</c> to a
/// browser on another device (see docs/specs/21-web-console.md). There is no route that changes
/// anything on the PC - see <see cref="WebConsoleRouter"/>.
/// </summary>
public interface IWebConsoleServer
{
    WebConsoleState State { get; }

    /// <summary>The port actually being listened on while <see cref="State"/> is
    /// <see cref="WebConsoleState.Running"/>; 0 otherwise.</summary>
    int Port { get; }

    /// <summary>User-readable reason for <see cref="WebConsoleState.Failed"/>; null otherwise.</summary>
    string? ErrorMessage { get; }

    /// <summary>Raised (on any thread) whenever <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Starts listening on <paramref name="port"/> on every network interface, requiring
    /// <paramref name="accessKey"/> for stats. Restarts if already running. Never throws for an
    /// unavailable port: <see cref="State"/> becomes <see cref="WebConsoleState.Failed"/> instead.</summary>
    void Start(int port, string accessKey);

    /// <summary>Stops listening and closes open connections. A no-op when not running.</summary>
    void Stop();
}
