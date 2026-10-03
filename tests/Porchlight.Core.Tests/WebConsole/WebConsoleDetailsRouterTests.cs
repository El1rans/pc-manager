using System.Text;
using System.Text.Json;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>The updates, startup and security views (spec 39): same read-only, key-protected rules as stats.</summary>
public class WebConsoleDetailsRouterTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly FakeStatsSource _stats = new();
    private readonly FakeDetailsSource _details = new();
    private readonly WebConsoleRouter _router;

    public WebConsoleDetailsRouterTests()
    {
        _router = new WebConsoleRouter(_stats, _details);
    }

    public static TheoryData<string> DetailPaths =>
        [WebConsoleRouter.UpdatesPath, WebConsoleRouter.StartupPath, WebConsoleRouter.SecurityPath];

    [Theory]
    [MemberData(nameof(DetailPaths))]
    public async Task Views_need_the_access_key(string path)
    {
        foreach (var key in new string?[] { null, string.Empty, "wrong", "0123456789abcdef0123456789abcdee" })
        {
            var response = await RouteAsync("GET", path, key);

            Assert.Equal(401, response.StatusCode);
            Assert.Contains(response.ExtraHeaders, h => h.Key == "WWW-Authenticate");
        }

        Assert.Equal(0, _details.CallCount);
    }

    [Theory]
    [MemberData(nameof(DetailPaths))]
    public async Task Views_are_read_only(string path)
    {
        foreach (var method in new[] { "POST", "PUT", "PATCH", "DELETE" })
        {
            var response = await RouteAsync(method, path, Key);

            Assert.Equal(405, response.StatusCode);
        }

        Assert.Equal(0, _details.CallCount);
    }

    [Fact]
    public async Task Updates_are_camel_case_json()
    {
        var response = await RouteAsync("GET", WebConsoleRouter.UpdatesPath, Key);

        Assert.Equal(200, response.StatusCode);
        Assert.StartsWith("application/json", response.ContentType, StringComparison.Ordinal);
        using var json = Parse(response);
        Assert.True(json.RootElement.GetProperty("hasChecked").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("2.0", json.RootElement.GetProperty("items")[0].GetProperty("availableVersion").GetString());
        Assert.Equal(FakeDetailsSource.HostileName, json.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Startup_impact_is_text()
    {
        var response = await RouteAsync("GET", WebConsoleRouter.StartupPath, Key);

        Assert.Equal(200, response.StatusCode);
        using var json = Parse(response);
        var item = json.RootElement.GetProperty("items")[0];
        Assert.Equal("High", item.GetProperty("impact").GetString());
        Assert.True(item.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task Security_has_headline_and_cards()
    {
        var response = await RouteAsync("GET", WebConsoleRouter.SecurityPath, Key);

        Assert.Equal(200, response.StatusCode);
        using var json = Parse(response);
        var root = json.RootElement;
        Assert.Equal("This PC looks safe", root.GetProperty("headline").GetString());
        Assert.Equal("Good", root.GetProperty("level").GetString());
        Assert.Equal("Attention", root.GetProperty("remoteAccess").GetProperty("level").GetString());
        Assert.Equal(1, root.GetProperty("protection").GetProperty("lines").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(DetailPaths))]
    public async Task Text_from_the_PC_is_escaped_in_the_json(string path)
    {
        var response = await RouteAsync("GET", path, Key);

        var raw = Encoding.UTF8.GetString(response.Body.Span);
        Assert.DoesNotContain('<', raw);
        Assert.DoesNotContain('>', raw);
        Assert.DoesNotContain("<script", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument Parse(WebConsoleResponse response) =>
        JsonDocument.Parse(Encoding.UTF8.GetString(response.Body.Span));

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
