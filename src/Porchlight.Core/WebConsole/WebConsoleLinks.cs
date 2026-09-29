using System.Globalization;

namespace Porchlight.Core.WebConsole;

/// <summary>Builds the address a browser opens to reach the web console.</summary>
public static class WebConsoleLinks
{
    /// <summary>E.g. <c>http://192.168.1.20:8765/#key=abc...</c>. The key rides in the #fragment,
    /// which browsers keep to themselves: the page reads it and sends it as a header, so it never
    /// appears in a request line, a proxy log or a Referer.</summary>
    public static string Format(string host, int port, string accessKey) =>
        string.Create(CultureInfo.InvariantCulture, $"http://{host}:{port}/#key={Uri.EscapeDataString(accessKey)}");
}
