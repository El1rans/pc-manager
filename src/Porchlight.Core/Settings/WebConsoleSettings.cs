using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the read-only web console feature (see
/// docs/specs/12-web-console.md).</summary>
public sealed class WebConsoleSettings
{
    /// <summary>Whether the web console is served. Off by default: it only listens on the network
    /// once the user turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>TCP port the web console listens on, on every network interface.</summary>
    public int Port { get; set; } = WebConsoleOptions.DefaultPort;

    /// <summary>Secret a browser must present to read any stats. Empty until the console is first
    /// turned on, when one is generated (see <see cref="AccessKeyGenerator"/>); regenerated on demand from the
    /// Web console page, which locks out every browser that had the old one.</summary>
    public string AccessKey { get; set; } = string.Empty;
}
