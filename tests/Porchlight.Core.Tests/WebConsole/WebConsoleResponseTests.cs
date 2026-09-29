using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class WebConsoleResponseTests
{
    [Fact]
    public void FormatHead_has_status_length_close_and_security_headers()
    {
        var response = WebConsoleResponse.Text(404, "Not Found", "Not found.") with
        {
            ExtraHeaders = [new("X-Extra", "1")],
        };

        var head = response.FormatHead();

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", head, StringComparison.Ordinal);
        Assert.Contains("\r\nContent-Length: 10\r\n", head, StringComparison.Ordinal);
        Assert.Contains("\r\nConnection: close\r\n", head, StringComparison.Ordinal);
        Assert.Contains("\r\nX-Content-Type-Options: nosniff\r\n", head, StringComparison.Ordinal);
        Assert.Contains("\r\nX-Frame-Options: DENY\r\n", head, StringComparison.Ordinal);
        Assert.Contains("\r\nContent-Security-Policy: default-src 'none';", head, StringComparison.Ordinal);
        Assert.Contains("\r\nX-Extra: 1\r\n", head, StringComparison.Ordinal);
        Assert.EndsWith("\r\n\r\n", head, StringComparison.Ordinal);
    }
}
