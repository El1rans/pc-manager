namespace Porchlight.Core.Winget;

/// <summary>How a <see cref="ReinstallWorkflow"/> run turned out.</summary>
public enum ReinstallOutcomeKind
{
    /// <summary>The old version was removed and the newest version was installed.</summary>
    Reinstalled,

    /// <summary>The uninstall step failed, so the install step never ran - the app is unchanged;
    /// nothing was lost. See <see cref="ReinstallOutcome.UninstallOutcome"/> for why.</summary>
    UninstallFailed,

    /// <summary>
    /// The uninstall step succeeded but the install step then failed - the critical state
    /// <c>docs/specs/09-friendly-update-outcomes.md</c> calls out: the app is no longer installed
    /// at all. The Updates page must offer "Try install again" (install only - never re-run the
    /// uninstall).
    /// </summary>
    InstallFailedAfterUninstall,
}
