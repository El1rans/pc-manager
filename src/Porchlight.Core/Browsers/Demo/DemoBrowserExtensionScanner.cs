#if DEBUG
namespace Porchlight.Core.Browsers.Demo;

/// <summary>DEBUG-only fake scanner: a made-up add-on list for documentation screenshots, so the
/// real machine's add-ons are never shown. Enabled by <c>PORCHLIGHT_DEMO_DATA=1</c>.</summary>
internal sealed class DemoBrowserExtensionScanner : IBrowserExtensionScanner
{
    private readonly TimeProvider _timeProvider;

    public DemoBrowserExtensionScanner(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<BrowserScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var extensions = new[]
        {
            Make(BrowserKind.Edge, "Person 1", "Spell checker", true, now.AddDays(-200), ExtensionSource.Store, ["storage"], []),
            Make(BrowserKind.Edge, "Person 1", "Coupon finder", true, now.AddDays(-3), ExtensionSource.Store, ["history", "downloads"], ["<all_urls>"]),
            Make(BrowserKind.Chrome, "Person 1", "Free PDF tools", true, now.AddDays(-40), ExtensionSource.Sideloaded, ["tabs"], ["*://*/*"]),
            Make(BrowserKind.Chrome, "Person 1", "Dark mode", false, now.AddDays(-90), ExtensionSource.Store, ["storage"], []),
            Make(BrowserKind.Firefox, "default-release", "Ad blocker", true, now.AddDays(-300), ExtensionSource.Store, ["webRequest"], ["<all_urls>"]),
        };

        var assessed = extensions
            .Select(e => new AssessedExtension(e, ExtensionRiskAssessor.Assess(e, now)))
            .ToList();
        return Task.FromResult(new BrowserScanResult(
            assessed,
            [BrowserKind.Edge, BrowserKind.Chrome, BrowserKind.Firefox],
            0));
    }

    private static InstalledExtension Make(
        BrowserKind browser,
        string profile,
        string name,
        bool enabled,
        DateTimeOffset installed,
        ExtensionSource source,
        string[] permissions,
        string[] hosts) =>
        new(browser, profile, "demo-" + name.Replace(' ', '-'), name, "Made-up example add-on.", "1.0", enabled,
            installed, source, permissions, hosts);
}
#endif
