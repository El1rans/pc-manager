#if DEBUG
namespace Porchlight.Core.Browsers.Demo;

/// <summary>DEBUG-only fake settings check: made-up findings for documentation screenshots, so the real
/// machine's browser settings are never shown. Enabled by <c>PORCHLIGHT_DEMO_DATA=1</c>.</summary>
internal sealed class DemoBrowserHijackScanner : IBrowserHijackScanner
{
    public Task<BrowserHijackResult> ScanAsync(CancellationToken cancellationToken)
    {
        const string profile = "Person 1";
        var findings = new List<HijackFinding>
        {
            new(BrowserKind.Edge, profile, HijackSetting.HomePage, HijackStatus.Ok, "msn.com"),
            new(BrowserKind.Edge, profile, HijackSetting.StartupPages, HijackStatus.Ok, BrowserHijackClassifier.DefaultValue),
            new(BrowserKind.Edge, profile, HijackSetting.SearchEngine, HijackStatus.Ok, "bing.com"),
            new(BrowserKind.Chrome, profile, HijackSetting.HomePage, HijackStatus.Changed, "fast-search-now.example"),
            new(BrowserKind.Chrome, profile, HijackSetting.StartupPages, HijackStatus.Ok, BrowserHijackClassifier.DefaultValue),
            new(BrowserKind.Chrome, null, HijackSetting.SearchEngine, HijackStatus.ForcedByPolicy, "search.better-results.example"),
            new(BrowserKind.Firefox, "default-release", HijackSetting.HomePage, HijackStatus.Ok, BrowserHijackClassifier.DefaultValue),
            new(BrowserKind.Firefox, "default-release", HijackSetting.SearchEngine, HijackStatus.Ok, "duckduckgo.com"),
        };
        return Task.FromResult(new BrowserHijackResult(
            findings, [BrowserKind.Edge, BrowserKind.Chrome, BrowserKind.Firefox], 0));
    }
}
#endif
