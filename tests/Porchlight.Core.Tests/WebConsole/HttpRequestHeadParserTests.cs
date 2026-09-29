using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class HttpRequestHeadParserTests
{
    [Fact]
    public void TryParse_reads_method_path_and_headers()
    {
        var ok = HttpRequestHeadParser.TryParse(
            "GET /api/stats?x=1 HTTP/1.1\r\nHost: pc:8765\r\nAuthorization: Bearer abc123",
            out var request);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/stats", request.Path);
        Assert.Equal("pc:8765", request.Headers["host"]);
        Assert.Equal("abc123", request.BearerToken);
    }

    [Fact]
    public void BearerToken_is_null_without_an_Authorization_header()
    {
        Assert.True(HttpRequestHeadParser.TryParse("GET / HTTP/1.1\r\nHost: pc", out var request));
        Assert.Null(request.BearerToken);
    }

    [Fact]
    public void BearerToken_is_null_for_another_scheme()
    {
        Assert.True(HttpRequestHeadParser.TryParse("GET / HTTP/1.1\r\nAuthorization: Basic abc", out var request));
        Assert.Null(request.BearerToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GET")]
    [InlineData("GET /")]
    [InlineData("GET / HTTP/2")]
    [InlineData("GET  / HTTP/1.1")]
    [InlineData("GET http://evil/ HTTP/1.1")]
    [InlineData("GET * HTTP/1.1")]
    [InlineData("G@T / HTTP/1.1")]
    [InlineData("GET /café HTTP/1.1")]
    [InlineData("GET / HTTP/1.1\r\nNoColonHere")]
    [InlineData("GET / HTTP/1.1\r\n: empty-name")]
    [InlineData("GET / HTTP/1.1\r\nBad Name: x")]
    [InlineData("GET / HTTP/1.1\r\nAuthorization: Bearer a\r\nauthorization: Bearer b")]
    public void TryParse_rejects_malformed_or_ambiguous_requests(string head) =>
        Assert.False(HttpRequestHeadParser.TryParse(head, out _));

    [Fact]
    public void TryParse_rejects_an_overlong_target()
    {
        var target = "/" + new string('a', WebConsoleOptions.MaxRequestTargetLength);
        Assert.False(HttpRequestHeadParser.TryParse($"GET {target} HTTP/1.1", out _));
    }
}
