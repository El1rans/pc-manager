using Porchlight.Core.Safety;
using Porchlight.Core.Startup;

namespace Porchlight.Core.WebConsole;

// The extra read-only views of the web console (spec 39). Each is its own small model rather than
// the Core type it is built from, so that nothing more than what the page shows (for example no
// program paths) can ever leave the PC. Every text value here can come from the PC (app names,
// publishers, tool names); the page inserts all of them with textContent, never as HTML.

/// <summary>Served by <c>GET /api/updates</c>: the app updates waiting at the last check made in
/// Porchlight. The console never runs <c>winget</c> itself.</summary>
/// <param name="HasChecked">False until Porchlight has checked for updates since it started.</param>
/// <param name="CheckedAt">When that check finished.</param>
/// <param name="Count">How many updates are waiting.</param>
/// <param name="Items">The updates.</param>
public sealed record WebConsoleUpdates(
    bool HasChecked,
    DateTimeOffset? CheckedAt,
    int Count,
    IReadOnlyList<WebConsoleUpdateItem> Items);

/// <summary>One waiting app update.</summary>
public sealed record WebConsoleUpdateItem(string Name, string InstalledVersion, string AvailableVersion);

/// <summary>Served by <c>GET /api/startup</c>: what starts at sign-in and how much it slows it down.</summary>
/// <param name="ImpactNeedsAdmin">True when Windows' startup trace could not be read, so no impact is known.</param>
/// <param name="Items">Startup items, slowest first.</param>
public sealed record WebConsoleStartup(bool ImpactNeedsAdmin, IReadOnlyList<WebConsoleStartupItem> Items);

/// <summary>One startup item.</summary>
public sealed record WebConsoleStartupItem(string Name, string? Publisher, bool IsEnabled, StartupImpact Impact);

/// <summary>Served by <c>GET /api/security</c>: the Safety page's picture of the PC.</summary>
/// <param name="Headline">One plain line, e.g. "This PC looks safe".</param>
/// <param name="Level">Overall result.</param>
/// <param name="Protection">Antivirus and firewall.</param>
/// <param name="WindowsUpdate">Windows Update status.</param>
/// <param name="RemoteAccess">Remote-control programs found.</param>
public sealed record WebConsoleSecurity(
    string Headline,
    SafetyLevel Level,
    WebConsoleSecurityCard Protection,
    WebConsoleSecurityCard WindowsUpdate,
    WebConsoleSecurityCard RemoteAccess);

/// <summary>One card of the security view.</summary>
/// <param name="Verdict">One plain line.</param>
/// <param name="Level">Good / Attention / Unknown.</param>
/// <param name="Lines">Supporting lines (products, tools, details).</param>
public sealed record WebConsoleSecurityCard(string Verdict, SafetyLevel Level, IReadOnlyList<string> Lines);
