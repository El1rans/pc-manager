using System.Globalization;
using System.Text;

namespace Porchlight.Core.WebConsole;

/// <summary>One HTTP response from the web console. The server always closes the connection
/// after sending it, and always adds <see cref="SecurityHeaders"/>.</summary>
/// <param name="StatusCode">HTTP status code, e.g. 200.</param>
/// <param name="ReasonPhrase">Status text, e.g. <c>OK</c>.</param>
/// <param name="ContentType">Value of the <c>Content-Type</c> header.</param>
/// <param name="Body">Response body (not sent for a <c>HEAD</c> request).</param>
/// <param name="ExtraHeaders">Headers specific to this response, e.g. <c>Allow</c> on a 405.</param>
public sealed record WebConsoleResponse(
    int StatusCode,
    string ReasonPhrase,
    string ContentType,
    ReadOnlyMemory<byte> Body,
    IReadOnlyList<KeyValuePair<string, string>> ExtraHeaders)
{
    /// <summary>Sent with every response: the page is only ever loaded from this server, never
    /// framed, never cached, and never leaks its URL (which can carry the access key) to anyone.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> SecurityHeaders =
    [
        new("Cache-Control", "no-store"),
        new("X-Content-Type-Options", "nosniff"),
        new("X-Frame-Options", "DENY"),
        new("Referrer-Policy", "no-referrer"),
        new(
            "Content-Security-Policy",
            "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; " +
            "base-uri 'none'; form-action 'none'; frame-ancestors 'none'"),
    ];

    /// <summary>A plain-text response with no extra headers.</summary>
    public static WebConsoleResponse Text(int statusCode, string reasonPhrase, string text) =>
        new(statusCode, reasonPhrase, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), []);

    /// <summary>The status line and every header, ending with the blank line that precedes the body.
    /// <c>Content-Length</c> always states the full body length, even for a <c>HEAD</c> request that
    /// does not send it.</summary>
    public string FormatHead()
    {
        var builder = new StringBuilder();
        builder.Append("HTTP/1.1 ").Append(StatusCode.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(ReasonPhrase).Append("\r\n");
        AppendHeader(builder, "Content-Type", ContentType);
        AppendHeader(builder, "Content-Length", Body.Length.ToString(CultureInfo.InvariantCulture));
        AppendHeader(builder, "Connection", "close");
        foreach (var header in SecurityHeaders)
        {
            AppendHeader(builder, header.Key, header.Value);
        }

        foreach (var header in ExtraHeaders)
        {
            AppendHeader(builder, header.Key, header.Value);
        }

        builder.Append("\r\n");
        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, string name, string value) =>
        builder.Append(name).Append(": ").Append(value).Append("\r\n");
}
