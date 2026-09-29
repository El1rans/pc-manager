namespace Porchlight.Core.WebConsole;

/// <summary>Whether the web console is serving right now.</summary>
public enum WebConsoleState
{
    Stopped,
    Running,

    /// <summary>It was asked to start but could not (e.g. the port is already in use) - see
    /// <see cref="IWebConsoleServer.ErrorMessage"/>.</summary>
    Failed,
}
