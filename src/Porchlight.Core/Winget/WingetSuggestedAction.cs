namespace Porchlight.Core.Winget;

/// <summary>What the Updates page should offer the user to do about a <see cref="WingetOutcome"/>.
/// A <c>[Flags]</c> enum rather than a single value: a "no applicable update" outcome for a
/// package winget refuses to upgrade (see <see cref="WingetExitCodes.UpdateNotApplicable"/>, e.g.
/// RARLab.WinRAR on the maintainer's PC) can offer both <see cref="Reinstall"/> and
/// <see cref="Hide"/> at once, so a row's actions are never limited to just one suggestion. See
/// <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
[Flags]
public enum WingetSuggestedAction
{
    /// <summary>Nothing to offer - either it already succeeded, or there is nothing the user can
    /// do about it from here.</summary>
    None = 0,

    /// <summary>Offer "Reinstall..." - uninstall then install the latest version
    /// (<see cref="ReinstallWorkflow"/>), behind a confirmation dialog.</summary>
    Reinstall = 1 << 0,

    /// <summary>Offer "Hide this update" - adds the package id to the ignore list.</summary>
    Hide = 1 << 1,

    /// <summary>Offer "Try again" - simply re-run the same operation.</summary>
    Retry = 1 << 2,

    /// <summary>Offer "Try again" with guidance to close the application first.</summary>
    CloseAppAndRetry = 1 << 3,

    /// <summary>Offer guidance to restart the PC before trying again.</summary>
    RestartPc = 1 << 4,
}
