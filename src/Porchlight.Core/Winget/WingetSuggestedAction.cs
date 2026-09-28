namespace Porchlight.Core.Winget;

/// <summary>What the Updates page should offer the user to do about a <see cref="WingetOutcome"/>.
/// See <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
public enum WingetSuggestedAction
{
    /// <summary>Nothing to offer - either it already succeeded, or there is nothing the user can
    /// do about it from here.</summary>
    None,

    /// <summary>Offer "Reinstall..." - uninstall then install the latest version
    /// (<see cref="ReinstallWorkflow"/>), behind a confirmation dialog.</summary>
    Reinstall,

    /// <summary>Offer "Hide this update" - adds the package id to the ignore list.</summary>
    Hide,

    /// <summary>Offer "Try again" - simply re-run the same operation.</summary>
    Retry,

    /// <summary>Offer "Try again" with guidance to close the application first.</summary>
    CloseAppAndRetry,

    /// <summary>Offer guidance to restart the PC before trying again.</summary>
    RestartPc,
}
