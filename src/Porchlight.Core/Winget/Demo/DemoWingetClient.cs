#if DEBUG
using Porchlight.Core.Processes;

namespace Porchlight.Core.Winget.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IWingetClient"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - reports a
/// fixed, made-up list of "available upgrades" so a documentation screenshot of the Updates page
/// never shows the real machine's installed apps. Includes one package per friendly-outcome case
/// from <c>docs/specs/09-friendly-update-outcomes.md</c> (<see cref="UpgradeResultsById"/>) so the
/// Status column screenshot shows real plain-language text, not just "Updated". Never starts a real
/// <c>winget</c> process.
/// </summary>
internal sealed class DemoWingetClient : IWingetClient
{
    private static readonly IReadOnlyList<WingetPackage> FakeUpgrades =
    [
        new WingetPackage("Demo Browser", "Demo.Browser", "120.0.1", "121.0.3", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Photo Viewer", "Demo.PhotoViewer", "4.2.0", "4.3.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Video Call", "Demo.VideoCall", "Unknown", "9.1.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Office Suite", "Demo.OfficeSuite", "2023.11", "2024.02", "winget", RequiresExplicit: true),
        new WingetPackage("Demo Shell Prompt", "Demo.ShellPrompt", "24.8.0", "31.3.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Archiver", "Demo.Archiver", "6.24.0", "7.23.0", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Chat Client", "Demo.ChatClient", "1.0.1", "1.0.2", "winget", RequiresExplicit: false),
        new WingetPackage("Demo Notes App", "Demo.NotesApp", "3.1.0", "3.2.0", "winget", RequiresExplicit: false),
    ];

    /// <summary>Canned <see cref="UpgradeAsync"/> exit codes, one per friendly-outcome case the
    /// demo screenshot needs to show - everything else defaults to a plain success.</summary>
    private static readonly Dictionary<string, int> UpgradeResultsById = new(StringComparer.OrdinalIgnoreCase)
    {
        // "Needs a reinstall" - Reinstall... - mirrors the maintainer's real JanDeDobbeleer.OhMyPosh case.
        ["Demo.ShellPrompt"] = WingetExitCodes.UpdateInstallTechnologyMismatch,

        // "Not available for this PC" - Hide this update - mirrors the maintainer's real RARLab.WinRAR case.
        ["Demo.Archiver"] = WingetExitCodes.UpdateNotApplicable,

        // "Close the app and try again" - Try again.
        ["Demo.ChatClient"] = WingetExitCodes.AppInUse,

        // "Updated - restart needed" - no action needed.
        ["Demo.NotesApp"] = WingetExitCodes.InstallRebootRequiredToFinish,
    };

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
        if (UpgradeResultsById.TryGetValue(id, out var exitCode))
        {
            var outcome = WingetExitCodes.DescribeOutcome(exitCode, outputLines: null);
            log?.Report($"Demo mode: pretending to upgrade {id} ({outcome.Title}).");
            progress?.Report("Done.");
            return Task.FromResult(new WingetResult(exitCode, [outcome.Explanation]));
        }

        log?.Report($"Demo mode: pretending to upgrade {id}.");
        progress?.Report("Done.");
        return Task.FromResult(new WingetResult(0, [$"Successfully installed {id} (demo mode)."]));
    }

    public Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken)
    {
        log?.Report($"Demo mode: no real package details for {id}.");
        return Task.FromResult(new WingetResult(0, [$"Id: {id}", "Demo mode - no real package details available."]));
    }

    public Task<WingetResult> UninstallAsync(string id, bool silent, IProgress<string>? log, CancellationToken cancellationToken)
    {
        log?.Report($"Demo mode: pretending to uninstall {id}.");
        return Task.FromResult(new WingetResult(0, [$"Successfully uninstalled {id} (demo mode)."]));
    }

    public Task<WingetResult> InstallAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        log?.Report($"Demo mode: pretending to install {id}.");
        progress?.Report("Done.");
        return Task.FromResult(new WingetResult(0, [$"Successfully installed {id} (demo mode)."]));
    }
}
#endif
