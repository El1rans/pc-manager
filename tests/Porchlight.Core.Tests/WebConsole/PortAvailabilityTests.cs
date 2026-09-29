using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class PortAvailabilityTests
{
    private readonly PortAvailability _availability = new(NullLogger<PortAvailability>.Instance);

    [Fact]
    public void A_port_held_by_another_listener_is_not_free()
    {
        using var other = new TcpListener(IPAddress.Loopback, 0);
        other.Start();
        var port = ((IPEndPoint)other.LocalEndpoint).Port;

        Assert.False(_availability.IsFree(port));
    }

    [Fact]
    public void A_released_port_is_free_again_and_the_check_does_not_hold_it()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
        }

        Assert.True(_availability.IsFree(port));
        Assert.True(_availability.IsFree(port));
    }
}
