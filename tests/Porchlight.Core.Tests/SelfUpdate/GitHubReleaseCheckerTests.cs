using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public class GitHubReleaseCheckerTests
{
    private sealed class FakeAppInfo(Version version) : IRunningAppInfo
    {
        public Version Version { get; } = version;

        public string? ExecutableDirectory => null;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json) };

    private static string Release(string tag) =>
        $$"""{ "tag_name": "{{tag}}", "html_url": "https://github.com/El1rans/porchlight/releases/tag/{{tag}}" }""";

    private static GitHubReleaseChecker Create(FakeHandler handler, string version = "0.1.0") =>
        new(handler, new FakeAppInfo(Version.Parse(version)), NullLogger<GitHubReleaseChecker>.Instance);

    [Fact]
    public async Task NewerRelease_ReportsUpdateAvailable()
    {
        var handler = new FakeHandler(_ => Json(Release("v0.2.0")));
        using var checker = Create(handler);

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.Equal(SelfUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(0, 2, 0), result.Release!.Version);
    }

    [Fact]
    public async Task SameOrOlderRelease_ReportsUpToDate()
    {
        using var same = Create(new FakeHandler(_ => Json(Release("v0.1.0"))));
        using var older = Create(new FakeHandler(_ => Json(Release("v0.0.9"))));

        Assert.Equal(SelfUpdateCheckStatus.UpToDate, (await same.CheckAsync(CancellationToken.None)).Status);
        Assert.Equal(SelfUpdateCheckStatus.UpToDate, (await older.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task SendsUserAgentWithVersionAndGitHubAcceptHeader_ToTheLatestReleaseEndpoint()
    {
        var handler = new FakeHandler(_ => Json(Release("v0.1.0")));
        using var checker = Create(handler, "0.1.0");

        await checker.CheckAsync(CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.github.com/repos/El1rans/porchlight/releases/latest", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Porchlight/0.1.0", request.Headers.UserAgent.ToString());
        Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorStatus_IsCouldNotCheck(HttpStatusCode status)
    {
        using var checker = Create(new FakeHandler(_ => Json("{}", status)));

        Assert.Equal(SelfUpdateCheckStatus.CouldNotCheck, (await checker.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task NoReleasesYet_IsUpToDate()
    {
        using var checker = Create(new FakeHandler(_ => Json("{}", HttpStatusCode.NotFound)));

        Assert.Equal(SelfUpdateCheckStatus.UpToDate, (await checker.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task NetworkFailure_IsCouldNotCheckAndDoesNotThrow()
    {
        using var checker = Create(new FakeHandler(_ => throw new HttpRequestException("offline")));

        Assert.Equal(SelfUpdateCheckStatus.CouldNotCheck, (await checker.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task MalformedTag_IsCouldNotCheck()
    {
        using var checker = Create(new FakeHandler(_ => Json(Release("nightly"))));

        Assert.Equal(SelfUpdateCheckStatus.CouldNotCheck, (await checker.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task PrereleaseInReply_IsIgnored()
    {
        using var checker = Create(new FakeHandler(_ => Json("""{ "tag_name": "v9.9.9", "prerelease": true }""")));

        Assert.Equal(SelfUpdateCheckStatus.CouldNotCheck, (await checker.CheckAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var checker = Create(new FakeHandler(_ => Json(Release("v0.2.0"))));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(cts.Token));
    }
}
