using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.WebConsole;

/// <inheritdoc cref="IPortAvailability"/>
public sealed partial class PortAvailability(ILogger<PortAvailability> logger) : IPortAvailability
{
    public bool IsFree(int port)
    {
        using var listener = WebConsoleListenerFactory.Create(port);
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException ex)
        {
            // Expected when another program holds the port; the caller simply tries the next one.
            LogPortBusy(port, ex.SocketErrorCode);
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Port {Port} is not free for the web console ({Error}).")]
    private partial void LogPortBusy(int port, SocketError error);
}
