using System.Net;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class WebConsoleLinksTests
{
    [Fact]
    public void Format_puts_the_key_in_the_fragment() =>
        Assert.Equal("http://192.168.1.20:8765/#key=abc", WebConsoleLinks.Format("192.168.1.20", 8765, "abc"));

    [Theory]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("100.101.102.103", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.10.20", false)]
    [InlineData("fe80::1", false)]
    [InlineData("::1", false)]
    public void IsReachableFromNetwork_keeps_only_useful_IPv4_addresses(string address, bool expected) =>
        Assert.Equal(expected, LocalAddressProvider.IsReachableFromNetwork(IPAddress.Parse(address)));

    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    [InlineData(WebConsoleOptions.DefaultPort, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    [InlineData(0, false)]
    public void IsAllowedPort_accepts_only_unprivileged_ports(int port, bool expected) =>
        Assert.Equal(expected, WebConsoleOptions.IsAllowedPort(port));
}
