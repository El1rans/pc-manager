using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class NetworkProbeTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }

    private sealed class NoStatus : INetworkStatusProvider
    {
        public NetworkStatus GetStatus() => NetworkStatus.Disconnected;
    }

    private static async Task<InternetCheckResult> Check(Func<HttpResponseMessage> respond)
    {
        using var probe = new NetworkProbe(new NoStatus(), new StubHandler(respond), NullLogger<NetworkProbe>.Instance);
        return await probe.CheckInternetAsync(TestContext.Current.CancellationToken);
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Expected_body_is_reachable() =>
        Assert.Equal(InternetCheckResult.Reachable, await Check(() => Ok("Microsoft Connect Test")));

    [Fact]
    public async Task Different_body_is_a_captive_portal() =>
        Assert.Equal(InternetCheckResult.CaptivePortal, await Check(() => Ok("<html>Please sign in</html>")));

    [Fact]
    public async Task Redirect_is_a_captive_portal() =>
        Assert.Equal(InternetCheckResult.CaptivePortal, await Check(() => new HttpResponseMessage(HttpStatusCode.Found)));

    [Fact]
    public async Task Server_error_is_unreachable() =>
        Assert.Equal(InternetCheckResult.Unreachable, await Check(() => new HttpResponseMessage(HttpStatusCode.BadGateway)));

    [Fact]
    public async Task Transport_failure_is_unreachable() =>
        Assert.Equal(InternetCheckResult.Unreachable, await Check(() => throw new HttpRequestException("offline")));

    [Fact]
    public async Task No_adapter_means_adapter_down_and_no_ping()
    {
        using var probe = new NetworkProbe(new NoStatus(), new StubHandler(() => Ok("")), NullLogger<NetworkProbe>.Instance);
        Assert.False(probe.IsAdapterUp());
        Assert.False(await probe.PingGatewayAsync(TestContext.Current.CancellationToken));
    }
}
