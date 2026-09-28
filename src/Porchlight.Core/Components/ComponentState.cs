namespace Porchlight.Core.Components;

/// <summary>Where a component (AnyDesk, OpenRGB, the PawnIO driver) currently stands.</summary>
public enum ComponentState
{
    /// <summary>Not found by any detection strategy.</summary>
    NotInstalled,

    /// <summary>Installed but not currently running (only meaningful for components that have a
    /// running/not-running distinction at all; others go straight from installed to "ready").</summary>
    Installed,

    /// <summary>Installed and its background process (or driver service) is active.</summary>
    Running,

    /// <summary>An install or start attempt failed; see <see cref="ComponentStatus.Message"/>.</summary>
    Error,
}
