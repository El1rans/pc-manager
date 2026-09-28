namespace Porchlight.Core.Winget;

/// <summary>Result of one <see cref="ReinstallWorkflow.RunAsync"/> call.</summary>
/// <param name="Kind">Which of the three shapes this run took.</param>
/// <param name="UninstallOutcome">Plain-language result of the uninstall step (always present -
/// the uninstall always runs first).</param>
/// <param name="InstallOutcome">Plain-language result of the install step, or null if the uninstall
/// failed and the install step never ran (<see cref="ReinstallOutcomeKind.UninstallFailed"/>).</param>
public sealed record ReinstallOutcome(
    ReinstallOutcomeKind Kind, WingetOutcome UninstallOutcome, WingetOutcome? InstallOutcome)
{
    /// <summary>Short label for the Status column - see <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
    public string Title => Kind switch
    {
        ReinstallOutcomeKind.Reinstalled => InstallOutcome!.Kind == WingetOutcomeKind.UpdatedRestartNeeded
            ? "Reinstalled - restart needed"
            : "Reinstalled",
        ReinstallOutcomeKind.UninstallFailed => "Reinstall didn't start",
        ReinstallOutcomeKind.InstallFailedAfterUninstall => "Not installed - the new version didn't install",
        _ => "Something went wrong",
    };

    /// <summary>One or two plain sentences for the tooltip and the log.</summary>
    public string Explanation => Kind switch
    {
        ReinstallOutcomeKind.Reinstalled => "The old version was removed and the newest version was installed.",
        ReinstallOutcomeKind.UninstallFailed =>
            $"The old version couldn't be removed, so nothing changed - the app is still installed. {UninstallOutcome.Explanation}",
        ReinstallOutcomeKind.InstallFailedAfterUninstall =>
            "The old version was removed, but the new version didn't install. " +
            (InstallOutcome?.Explanation ?? string.Empty),
        _ => string.Empty,
    };

    /// <summary>What the Updates page should offer next.</summary>
    public WingetSuggestedAction SuggestedAction => Kind switch
    {
        ReinstallOutcomeKind.Reinstalled => WingetSuggestedAction.None,
        ReinstallOutcomeKind.UninstallFailed => WingetSuggestedAction.Retry,
        ReinstallOutcomeKind.InstallFailedAfterUninstall => WingetSuggestedAction.Retry,
        _ => WingetSuggestedAction.None,
    };

    /// <summary>True for the "old version gone, new one didn't install" state that
    /// <c>docs/specs/09-friendly-update-outcomes.md</c> calls out as needing to be "clearly
    /// reported" - the app is currently not installed at all.</summary>
    public bool IsCritical => Kind == ReinstallOutcomeKind.InstallFailedAfterUninstall;
}
