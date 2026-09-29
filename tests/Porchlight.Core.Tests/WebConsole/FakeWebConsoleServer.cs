using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>Records <see cref="Start"/>/<see cref="Stop"/> calls instead of opening a socket.</summary>
internal sealed class FakeWebConsoleServer : IWebConsoleServer
{
    public WebConsoleState State { get; private set; }

    public int Port { get; private set; }

    public string? ErrorMessage => null;

    public string? LastAccessKey { get; private set; }

    public int StartCount { get; private set; }

    public event EventHandler? StateChanged;

    public void Start(int port, string accessKey)
    {
        StartCount++;
        State = WebConsoleState.Running;
        Port = port;
        LastAccessKey = accessKey;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        State = WebConsoleState.Stopped;
        Port = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
