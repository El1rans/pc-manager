using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>End-to-end over a real loopback socket: proves the listener, request reading and
/// response writing work together with an actual HTTP client.</summary>
public sealed class WebConsoleServerTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly WebConsoleServer _server =
        new(new WebConsoleRouter(new FakeStatsSource(), new FakeDetailsSource()), NullLogger<WebConsoleServer>.Instance);

    private readonly HttpClient _client = new(new SocketsHttpHandler { UseProxy = false });

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Serves_the_page_and_stats_with_the_key()
    {
        _server.Start(0, Key);
        Assert.Equal(WebConsoleState.Running, _server.State);
        Assert.NotEqual(0, _server.Port);

        using var page = await _client.GetAsync(Url("/"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("app.js", await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Get, Url(WebConsoleRouter.StatsPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        using var stats = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        Assert.Contains("DEMO-PC", await stats.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_stats_without_the_key_and_any_write()
    {
        _server.Start(0, Key);

        using var noKey = await _client.GetAsync(Url(WebConsoleRouter.StatsPath), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var post = await _client.PostAsync(Url(WebConsoleRouter.StatsPath), content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task Answers_garbage_with_bad_request()
    {
        _server.Start(0, Key);

        var response = await SendRawAsync("NOT HTTP AT ALL\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 400 ", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Head_requests_get_headers_only()
    {
        _server.Start(0, Key);

        var response = await SendRawAsync("HEAD / HTTP/1.1\r\nHost: localhost\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response, StringComparison.Ordinal);
        Assert.EndsWith("\r\n\r\n", response, StringComparison.Ordinal);
    }

    [Fact]
    public void A_port_already_in_use_fails_without_throwing()
    {
        _server.Start(0, Key);
        using var second = new WebConsoleServer(new WebConsoleRouter(new FakeStatsSource(), new FakeDetailsSource()), NullLogger<WebConsoleServer>.Instance);

        second.Start(_server.Port, Key);

        Assert.Equal(WebConsoleState.Failed, second.State);
        Assert.False(string.IsNullOrEmpty(second.ErrorMessage));
    }

    [Fact]
    public async Task Stop_closes_the_port_and_raises_StateChanged()
    {
        var changes = 0;
        _server.StateChanged += (_, _) => changes++;
        _server.Start(0, Key);
        var port = _server.Port;

        _server.Stop();

        Assert.Equal(WebConsoleState.Stopped, _server.State);
        Assert.Equal(0, _server.Port);
        Assert.Equal(2, changes);
        using var client = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(
            () => client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken).AsTask());
    }

    private Uri Url(string path) => new($"http://127.0.0.1:{_server.Port}{path}");

    private async Task<string> SendRawAsync(string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port, TestContext.Current.CancellationToken);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
