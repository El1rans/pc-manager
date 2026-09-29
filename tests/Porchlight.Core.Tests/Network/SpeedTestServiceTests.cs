using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class SpeedTestServiceTests
{
    /// <summary>Fake transport: every request advances the fake clock, no real network.</summary>
    private sealed class FakeHandler(FakeTimeProvider clock, TimeSpan perRequest, bool fail = false) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.Method + " " + request.RequestUri);
            if (fail)
            {
                throw new HttpRequestException("offline");
            }

            clock.Advance(perRequest);
            var bytes = 0;
            if (request.RequestUri!.Query.StartsWith("?bytes=", StringComparison.Ordinal))
            {
                bytes = (int)Math.Min(long.Parse(request.RequestUri.Query[7..], System.Globalization.CultureInfo.InvariantCulture), 1_000_000);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[bytes]) });
        }
    }

    [Fact]
    public async Task Measures_latency_download_and_upload_from_the_cloudflare_endpoints()
    {
        var clock = new FakeTimeProvider();
        var handler = new FakeHandler(clock, TimeSpan.FromMilliseconds(100));
        using var service = new SpeedTestService(handler, clock, NullLogger<SpeedTestService>.Instance);

        var result = await service.RunAsync(null, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(100, result.LatencyMs);
        Assert.True(result.DownloadMbps > 0);
        Assert.True(result.UploadMbps > 0);
        Assert.Contains(handler.Urls, u => u.StartsWith("GET https://speed.cloudflare.com/__down?bytes=", StringComparison.Ordinal));
        Assert.Contains("POST https://speed.cloudflare.com/__up", handler.Urls);
        Assert.All(handler.Urls, u => Assert.Contains("speed.cloudflare.com", u, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_phases_in_order()
    {
        var clock = new FakeTimeProvider();
        using var service = new SpeedTestService(
            new FakeHandler(clock, TimeSpan.FromMilliseconds(50)), clock, NullLogger<SpeedTestService>.Instance);
        var phases = new List<SpeedTestPhase>();

        await service.RunAsync(new Sync(phases), TestContext.Current.CancellationToken);

        Assert.Equal([SpeedTestPhase.Latency, SpeedTestPhase.Download, SpeedTestPhase.Upload], phases);
    }

    [Fact]
    public async Task Returns_null_when_everything_fails()
    {
        var clock = new FakeTimeProvider();
        using var service = new SpeedTestService(
            new FakeHandler(clock, TimeSpan.Zero, fail: true), clock, NullLogger<SpeedTestService>.Instance);

        Assert.Null(await service.RunAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        var clock = new FakeTimeProvider();
        using var service = new SpeedTestService(
            new FakeHandler(clock, TimeSpan.FromMilliseconds(10)), clock, NullLogger<SpeedTestService>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(null, cts.Token));
    }

    private sealed class Sync(List<SpeedTestPhase> target) : IProgress<SpeedTestPhase>
    {
        public void Report(SpeedTestPhase value) => target.Add(value);
    }
}
