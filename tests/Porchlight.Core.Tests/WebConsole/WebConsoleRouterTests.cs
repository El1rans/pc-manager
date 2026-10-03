using System.Text;
using System.Text.Json;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class WebConsoleRouterTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly FakeStatsSource _stats = new();
    private readonly FakeDetailsSource _details = new();
    private readonly WebConsoleRouter _router;

    public WebConsoleRouterTests()
    {
        _router = new WebConsoleRouter(_stats, _details);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task Anything_but_GET_or_HEAD_is_refused_on_every_path(string method)
    {
        foreach (var path in new[] { "/", WebConsoleRouter.StatsPath, WebConsoleRouter.UpdatesPath, WebConsoleRouter.StartupPath, WebConsoleRouter.SecurityPath, "/anything" })
        {
            var response = await RouteAsync(method, path, Key);

            Assert.Equal(405, response.StatusCode);
            Assert.Contains(response.ExtraHeaders, h => h.Key == "Allow" && h.Value == "GET, HEAD");
        }

        Assert.Equal(0, _stats.CallCount);
    }

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/index.html", "text/html")]
    [InlineData("/app.js", "text/javascript")]
    [InlineData("/app.css", "text/css")]
    public async Task Page_files_are_served_without_a_key(string path, string contentType)
    {
        var response = await RouteAsync("GET", path, key: null);

        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith(contentType, response.ContentType, StringComparison.Ordinal);
        Assert.False(response.Body.IsEmpty);
    }

    [Fact]
    public async Task Unknown_paths_are_not_found()
    {
        var response = await RouteAsync("GET", "/api/settings", Key);

        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData("0123456789abcdef0123456789abcdee")]
    public async Task Stats_need_the_access_key(string? key)
    {
        var response = await RouteAsync("GET", WebConsoleRouter.StatsPath, key);

        Assert.Equal(401, response.StatusCode);
        Assert.Contains(response.ExtraHeaders, h => h.Key == "WWW-Authenticate");
        Assert.Equal(0, _stats.CallCount);
    }

    [Fact]
    public async Task Stats_are_refused_when_no_key_has_been_set_up()
    {
        var response = await _router.RouteAsync(
            new HttpRequestHead("GET", WebConsoleRouter.StatsPath, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer ",
            }),
            accessKey: string.Empty,
            TestContext.Current.CancellationToken);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Stats_with_the_key_are_camel_case_json()
    {
        var response = await RouteAsync("HEAD", WebConsoleRouter.StatsPath, Key);
        Assert.Equal(200, response.StatusCode);

        response = await RouteAsync("GET", WebConsoleRouter.StatsPath, Key);

        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith("application/json", response.ContentType, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(response.Body.Span));
        var root = json.RootElement;
        Assert.Equal("DEMO-PC", root.GetProperty("systemInfo").GetProperty("computerName").GetString());
        Assert.Equal(12.5, root.GetProperty("performance").GetProperty("cpuPercent").GetDouble());
        Assert.True(root.GetProperty("isRestartPending").GetBoolean());
        Assert.Equal("Caution", root.GetProperty("hardware")[0].GetProperty("severity").GetString());
        Assert.True(root.GetProperty("drives")[0].GetProperty("isLow").GetBoolean());
    }

    private Task<WebConsoleResponse> RouteAsync(string method, string path, string? key)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (key is not null)
        {
            headers["Authorization"] = "Bearer " + key;
        }

        return _router.RouteAsync(new HttpRequestHead(method, path, headers), Key, TestContext.Current.CancellationToken);
    }
}
