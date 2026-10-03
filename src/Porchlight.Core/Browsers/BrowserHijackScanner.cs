using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserHijackScanner"/>
public sealed class BrowserHijackScanner : IBrowserHijackScanner
{
    private readonly IBrowserLocations _locations;
    private readonly IBrowserPolicyReader _policies;
    private readonly ILogger<BrowserHijackScanner> _logger;

    public BrowserHijackScanner(
        IBrowserLocations locations,
        IBrowserPolicyReader policies,
        ILogger<BrowserHijackScanner> logger)
    {
        _locations = locations;
        _policies = policies;
        _logger = logger;
    }

    public Task<BrowserHijackResult> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private BrowserHijackResult Scan(CancellationToken cancellationToken)
    {
        var tally = new ScanTally();
        var found = new List<BrowserKind>();
        var findings = new List<HijackFinding>();

        var chromium = new ChromiumSettingsReader(_logger, tally);
        foreach (var location in _locations.ChromiumBrowsers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ChromiumExtensionReader.IsInstalled(location))
            {
                continue;
            }

            found.Add(location.Kind);
            var policy = _policies.Read(location.Kind);
            AddPolicyFindings(location.Kind, policy, findings);
            try
            {
                foreach (var profile in chromium.Read(location, cancellationToken))
                {
                    AddProfileFindings(location.Kind, profile, policy, findings);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read {Browser} profiles.", location.Kind);
                tally.CountSkipped();
            }
        }

        var firefoxDir = _locations.FirefoxProfilesDirectory;
        if (firefoxDir is not null && FirefoxExtensionReader.IsInstalled(firefoxDir))
        {
            found.Add(BrowserKind.Firefox);
            try
            {
                foreach (var profile in new FirefoxSettingsReader(_logger, tally).Read(firefoxDir, cancellationToken))
                {
                    AddProfileFindings(BrowserKind.Firefox, profile, BrowserPolicySettings.None, findings);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read Firefox profiles.");
                tally.CountSkipped();
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Browser settings check: {Count} settings in {Browsers} browsers, {Unusual} unusual, {Skipped} skipped.",
                findings.Count,
                found.Count,
                findings.Count(f => f.Status != HijackStatus.Ok),
                tally.Skipped);
        }

        return new BrowserHijackResult(findings, found, tally.Skipped);
    }

    /// <summary>Browser-wide findings for settings a policy forces (shown once per browser).</summary>
    private static void AddPolicyFindings(BrowserKind browser, BrowserPolicySettings policy, List<HijackFinding> findings)
    {
        if (policy.HomePage is not null)
        {
            Add(findings, browser, null, HijackSetting.HomePage, BrowserHijackClassifier.Classify(policy.HomePage, forcedByPolicy: true));
        }

        if (policy.StartupUrls.Count > 0)
        {
            Add(findings, browser, null, HijackSetting.StartupPages, BrowserHijackClassifier.ClassifyAll(policy.StartupUrls, forcedByPolicy: true));
        }

        if (policy.NewTabPage is not null)
        {
            Add(findings, browser, null, HijackSetting.NewTabPage, BrowserHijackClassifier.Classify(policy.NewTabPage, forcedByPolicy: true));
        }

        if (policy.SearchUrl is not null)
        {
            Add(findings, browser, null, HijackSetting.SearchEngine, BrowserHijackClassifier.Classify(policy.SearchUrl, forcedByPolicy: true));
        }
    }

    /// <summary>Per-profile findings; a setting that a policy overrides is left out (the browser ignores
    /// the profile's own value, and the policy finding already covers it).</summary>
    private static void AddProfileFindings(
        BrowserKind browser, ProfileSettings profile, BrowserPolicySettings policy, List<HijackFinding> findings)
    {
        if (policy.HomePage is null)
        {
            Add(findings, browser, profile.ProfileName, HijackSetting.HomePage, profile.HomePage);
        }

        if (policy.StartupUrls.Count == 0)
        {
            Add(findings, browser, profile.ProfileName, HijackSetting.StartupPages, profile.StartupPages);
        }

        if (profile.NewTabPage is not null && policy.NewTabPage is null)
        {
            Add(findings, browser, profile.ProfileName, HijackSetting.NewTabPage, profile.NewTabPage);
        }

        if (policy.SearchUrl is null)
        {
            Add(findings, browser, profile.ProfileName, HijackSetting.SearchEngine, profile.SearchEngine);
        }
    }

    private static void Add(
        List<HijackFinding> findings,
        BrowserKind browser,
        string? profile,
        HijackSetting setting,
        HijackClassification classification) =>
        findings.Add(new HijackFinding(browser, profile, setting, classification.Status, classification.Value));
}
