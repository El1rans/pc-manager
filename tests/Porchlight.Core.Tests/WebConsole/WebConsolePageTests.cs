using System.Text;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>The page's own files: the browser side of "values from the PC are shown as text".</summary>
public class WebConsolePageTests
{
    private readonly WebConsoleRouter _router = new(new FakeStatsSource(), new FakeDetailsSource());

    [Theory]
    [InlineData("innerHTML")]
    [InlineData("outerHTML")]
    [InlineData("insertAdjacentHTML")]
    [InlineData("document.write")]
    [InlineData("eval(")]
    [InlineData("new Function")]
    public async Task Script_never_inserts_markup(string forbidden)
    {
        var script = await GetAsync("/app.js");

        Assert.DoesNotContain(forbidden, script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Script_only_reads_and_the_page_links_the_new_views()
    {
        var script = await GetAsync("/app.js");
        var page = await GetAsync("/");

        Assert.DoesNotContain("method:", script, StringComparison.Ordinal);
        Assert.DoesNotContain("POST", script, StringComparison.Ordinal);
        foreach (var path in new[] { "api/updates", "api/startup", "api/security" })
        {
            Assert.Contains(path, script, StringComparison.Ordinal);
        }

        foreach (var target in new[] { "security-card", "updates-card", "startup-card" })
        {
            Assert.Contains($"id=\"{target}\"", page, StringComparison.Ordinal);
            Assert.Contains($"data-target=\"{target}\"", page, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<form", page, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetAsync(string path)
    {
        var response = await _router.RouteAsync(
            new HttpRequestHead("GET", path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            "key",
            TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(response.Body.Span);
    }
}
