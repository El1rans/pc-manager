namespace Porchlight.Core.Winget.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IWingetClient"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - reports a
/// fixed, made-up list of "available upgrades" so a documentation screenshot of the Updates page
/// never shows the real machine's installed apps. Never starts a real <c>winget</c> process.
/// </summary>
internal sealed class DemoWingetClient : IWingetClient
{
    private static readonly IReadOnlyList<WingetPackage> FakeUpgrades =
    [
        new WingetPackage("Demo Browser", "Demo.Browser", "120.0.1", "121.0.3", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Photo Viewer", "Demo.PhotoViewer", "4.2.0", "4.3.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Video Call", "Demo.VideoCall", "Unknown", "9.1.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Office Suite", "Demo.OfficeSuite", "2023.11", "2024.02", "winget", RequiresExplicit: true),
    ];

    public Task<IReadOnlyList<WingetPackage>> GetUpgradesAsync(
        bool includeUnknown, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Checking for updates...");
        IReadOnlyList<WingetPackage> upgrades = includeUnknown
            ? FakeUpgrades
            : [.. FakeUpgrades.Where(p => p.InstalledVersion != "Unknown")];
        return Task.FromResult(upgrades);
    }

    public Task<WingetResult> UpgradeAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        log?.Report($"Demo mode: pretending to upgrade {id}.");
        progress?.Report("Done.");
        return Task.FromResult(new WingetResult(0, [$"Successfully installed {id} (demo mode)."]));
    }

    public Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken)
    {
        log?.Report($"Demo mode: no real package details for {id}.");
        return Task.FromResult(new WingetResult(0, [$"Id: {id}", "Demo mode - no real package details available."]));
    }
}
