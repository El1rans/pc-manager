using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the read-only web console feature (see
/// docs/specs/21-web-console.md).</summary>
public sealed class WebConsoleSettings
{
    /// <summary>Whether the web console is served. Off by default: it only listens on the network
    /// once the user turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>TCP port the web console listens on, on every network interface.</summary>
    public int Port { get; set; } = WebConsoleOptions.DefaultPort;

    /// <summary>True once the user has picked <see cref="Port"/> themselves. Until then, the first
    /// time the console starts it may move off a busy default to the nearest free port (see
    /// <see cref="WebConsoleController.ApplySettings"/>); a port the user chose is never changed.</summary>
    public bool PortChosenByUser { get; set; }

    /// <summary>Secret a browser must present to read any stats. Empty until the console is first
    /// turned on, when one is generated (see <see cref="AccessKeyGenerator"/>); regenerated on demand from the
    /// Web console page, which locks out every browser that had the old one.</summary>
    public string AccessKey { get; set; } = string.Empty;
}
