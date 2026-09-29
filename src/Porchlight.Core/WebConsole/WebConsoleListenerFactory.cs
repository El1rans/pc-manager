using System.Net;
using System.Net.Sockets;

namespace Porchlight.Core.WebConsole;

/// <summary>Creates the web console's listener, shared by <see cref="WebConsoleServer"/> and
/// <see cref="PortAvailability"/> so the free-port check binds exactly the way the server will.</summary>
internal static class WebConsoleListenerFactory
{
    /// <summary>Listens on every interface - IPv6 and IPv4 together where IPv6 is available.</summary>
    public static TcpListener Create(int port)
    {
        TcpListener listener;
        if (Socket.OSSupportsIPv6)
        {
            listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.DualMode = true;
        }
        else
        {
            listener = new TcpListener(IPAddress.Any, port);
        }

        // Stops another program binding the same port more specifically and intercepting requests.
        listener.ExclusiveAddressUse = true;
        return listener;
    }
}
