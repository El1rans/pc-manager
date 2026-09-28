namespace Porchlight.Core.Winget;

/// <summary>Broad category of what happened when winget acted on one package (an upgrade,
/// uninstall, or install) - drives the Updates page's Status column icon/color and which
/// <see cref="WingetSuggestedAction"/> makes sense. See <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
public enum WingetOutcomeKind
{
    /// <summary>Succeeded, and nothing further is needed.</summary>
    Updated,

    /// <summary>Succeeded, but a restart is needed before it takes effect.</summary>
    UpdatedRestartNeeded,

    /// <summary>Winget has nothing to do for this package right now (already up to date, no
    /// applicable installer for this system, or pinned).</summary>
    NoApplicableUpdate,

    /// <summary>The available update uses a different install technology than what is currently
    /// installed, so a plain upgrade cannot apply it - an uninstall-then-install (see
    /// <see cref="ReinstallWorkflow"/>) is needed instead.</summary>
    ReinstallRequired,

    /// <summary>The target application (or one of its files) is currently running/in use.</summary>
    AppRunning,

    /// <summary>The user (or an elevation/consent prompt) cancelled the operation.</summary>
    Cancelled,

    /// <summary>The operation needs administrator approval that was not available.</summary>
    NeedsAdmin,

    /// <summary>Blocked by an organization policy (Group Policy / MDM), not by anything the user
    /// can fix themselves.</summary>
    Blocked,

    /// <summary>A download, network, or file-integrity problem stopped the operation.</summary>
    NetworkProblem,

    /// <summary>Anything else, including every exit code this app does not specifically recognize.</summary>
    Failed,
}
