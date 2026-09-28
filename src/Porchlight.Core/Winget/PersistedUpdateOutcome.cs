using System.Globalization;

namespace Porchlight.Core.Winget;

/// <summary>
/// One remembered outcome from a package's last attempted update/reinstall, persisted (via
/// <c>Settings.UpdatesSettings.LastOutcomes</c>) so the Updates page's row actions
/// (Reinstall.../Hide/Try again) survive an app restart instead of only showing for the session
/// that actually produced the failure. See <c>docs/specs/09-friendly-update-outcomes.md</c>'s
/// "Remember last outcome across restarts" addendum.
/// </summary>
/// <remarks>
/// Kept as its own plain mutable class - not a direct reuse of <see cref="WingetOutcome"/> - for
/// two reasons: every other settings section in <c>AppSettings</c> is a plain mutable POCO (so
/// JSON round-trips the same predictable way everywhere), and this type needs one field
/// (<see cref="IsCriticalReinstallFailure"/>) that has no <see cref="WingetOutcome"/> equivalent -
/// it comes from a <see cref="ReinstallOutcome"/> or an install-only retry instead.
/// </remarks>
public sealed class PersistedUpdateOutcome
{
    /// <summary>Winget package id - half of the match key (see <see cref="UpdateOutcomeMemory"/>).</summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>The <c>AvailableVersion</c> this outcome was produced against - the other half of
    /// the match key. A row is only shown a remembered outcome while winget still reports the same
    /// available version; once it changes, the old outcome no longer applies and is dropped.</summary>
    public string AvailableVersion { get; set; } = string.Empty;

    public WingetOutcomeKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The exact text the row's Status column tooltip showed when this was applied live -
    /// already includes the winget code where the live path would have shown one (e.g.
    /// <see cref="WingetOutcome.TooltipText"/>), so this is restored verbatim rather than
    /// reformatted - the three paths that persist an outcome
    /// (<c>UpdatesViewModel.PersistUpgradeOutcome/PersistReinstallOutcome/PersistInstallOnlyOutcome</c>)
    /// each build their tooltip text differently, and re-deriving it here would have to duplicate
    /// that logic and could drift out of sync with it.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>The raw winget exit code this was produced from - kept for the log only; never
    /// reformatted into <see cref="Explanation"/> again on restore (see its remarks).</summary>
    public int ExitCode { get; set; }

    public WingetSuggestedAction SuggestedAction { get; set; }

    /// <summary>Mirrors <c>Porchlight.App.Features.Updates.UpdatePackageViewModel.IsCriticalReinstallFailure</c>.</summary>
    public bool IsCriticalReinstallFailure { get; set; }
}
