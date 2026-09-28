using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

/// <summary>One row of the Updates page's DataGrid: a <see cref="WingetPackage"/> plus the
/// selection/ignore/live-status state the page tracks for it. <see cref="StatusText"/> is always a
/// short plain-language <see cref="WingetOutcome.Title"/> (or the legacy "Queued"/"Updating..."
/// step text) - never a bare hex code; the code only ever shows up in <see cref="StatusTooltip"/>.
/// See <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
public sealed partial class UpdatePackageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Notes))]
    [NotifyPropertyChangedFor(nameof(IsHiddenByDefault))]
    private bool _isIgnored;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    private UpdateRowState _state = UpdateRowState.None;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Tooltip and <c>AutomationProperties.HelpText</c> for the Status column - the
    /// explanation plus "(winget code 0x...)", or null while nothing has run yet.</summary>
    [ObservableProperty]
    private string? _statusTooltip;

    /// <summary>What the row's action area should offer for the current <see cref="StatusText"/> -
    /// see <c>docs/specs/09-friendly-update-outcomes.md</c>'s row actions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReinstall))]
    [NotifyPropertyChangedFor(nameof(CanHide))]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    private WingetSuggestedAction _suggestedAction = WingetSuggestedAction.None;

    /// <summary>True only for the critical "uninstalled but the new version didn't install" state
    /// (<see cref="ReinstallOutcomeKind.InstallFailedAfterUninstall"/>) - changes the retry button's
    /// label to "Try install again" and never re-runs the uninstall step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RetryButtonText))]
    private bool _isCriticalReinstallFailure;

    /// <summary>True when winget reported this package updated successfully while its Installed
    /// version was "Unknown" - Porchlight cannot tell whether that actually replaced the old
    /// install or added a second copy alongside it (observed for Google.CloudSDK: a per-machine
    /// 586.0.0 copy already present, then a second per-user copy installed). The row is hidden by
    /// default (like an ignored row - see <see cref="IsHiddenByDefault"/>) but still discoverable
    /// via "Show ignored", and reappears on its own once winget reports a newer available version -
    /// see <c>UpdatesViewModel.PersistUpgradeOutcome</c> and
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Notes))]
    [NotifyPropertyChangedFor(nameof(IsHiddenByDefault))]
    private bool _isPendingVersionConfirmation;

    /// <summary>Set while this row's winget operation has been running longer than
    /// <c>UpdatesViewModel.WaitingHintThreshold</c> with no result yet - "Still working. The
    /// installer may be waiting for you..." or, when Silent install is on, "Still installing. Large
    /// apps can take several minutes." Null the rest of the time. See
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
    [ObservableProperty]
    private string? _waitingHint;

    public UpdatePackageViewModel(WingetPackage package)
    {
        Package = package;
    }

    public WingetPackage Package { get; }

    public string Name => Package.Name;

    public string Id => Package.Id;

    public string InstalledVersion => Package.InstalledVersion;

    public string AvailableVersion => Package.AvailableVersion;

    public bool RequiresExplicit => Package.RequiresExplicit;

    /// <summary>"Ignored" / "Already updated (unconfirmed)" / "Pinned / explicit only" / "Current
    /// version unknown - updating may install a second copy", in that priority order - see
    /// <c>docs/specs/02-updates.md</c> and, for the second one,
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    public string Notes
    {
        get
        {
            if (IsIgnored)
            {
                return "Ignored";
            }

            if (IsPendingVersionConfirmation)
            {
                return $"Already updated to {AvailableVersion} - shown here in case a second copy was installed";
            }

            if (RequiresExplicit)
            {
                return "Pinned / explicit only";
            }

            if (InstalledVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "Current version unknown - updating may install a second copy";
            }

            return string.Empty;
        }
    }

    /// <summary>True for a row that should be hidden unless "Show ignored" is on - either the user
    /// explicitly ignored it (<see cref="IsIgnored"/>), or it is a not-yet-confirmed "already
    /// updated" row for a package whose installed version is unknown
    /// (<see cref="IsPendingVersionConfirmation"/>). Both are hidden the same way so the latter
    /// stays discoverable without a second toggle - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    public bool IsHiddenByDefault => IsIgnored || IsPendingVersionConfirmation;

    public string StatusGlyph => State switch
    {
        UpdateRowState.Updated => "", // checkmark
        UpdateRowState.Failed => "", // error
        UpdateRowState.Updating => "", // sync/progress
        UpdateRowState.Skipped => "", // warning
        UpdateRowState.Queued => "", // clock
        _ => string.Empty,
    };

    /// <summary>"Reinstall..." row action - see <see cref="WingetSuggestedAction.Reinstall"/>.
    /// <see cref="WingetSuggestedAction"/> is a <c>[Flags]</c> enum, so this and
    /// <see cref="CanHide"/> can both be true at once (e.g. "no applicable update" offers both) -
    /// see <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    public bool CanReinstall => SuggestedAction.HasFlag(WingetSuggestedAction.Reinstall);

    /// <summary>"Hide this update" row action - see <see cref="WingetSuggestedAction.Hide"/>.</summary>
    public bool CanHide => SuggestedAction.HasFlag(WingetSuggestedAction.Hide);

    /// <summary>"Try again" / "Try install again" row action - see
    /// <see cref="WingetSuggestedAction.Retry"/> and <see cref="WingetSuggestedAction.CloseAppAndRetry"/>.</summary>
    public bool CanRetry =>
        SuggestedAction.HasFlag(WingetSuggestedAction.Retry) || SuggestedAction.HasFlag(WingetSuggestedAction.CloseAppAndRetry);

    /// <summary>"Try again" normally, "Try install again" for <see cref="IsCriticalReinstallFailure"/>.</summary>
    public string RetryButtonText => IsCriticalReinstallFailure ? "Try install again" : "Try again";

    /// <summary>Applies a plain <see cref="WingetOutcome"/> (an upgrade or a plain install/uninstall
    /// that succeeded) to this row's Status column.</summary>
    public void ApplyOutcome(WingetOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        StatusText = outcome.Title;
        StatusTooltip = outcome.TooltipText;
        SuggestedAction = outcome.SuggestedAction;
        IsCriticalReinstallFailure = false;
        State = outcome.Kind switch
        {
            WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded => UpdateRowState.Updated,
            WingetOutcomeKind.NoApplicableUpdate => UpdateRowState.Skipped,
            _ => UpdateRowState.Failed,
        };
    }

    /// <summary>Applies a <see cref="ReinstallOutcome"/> (from the "Reinstall..." action) to this
    /// row's Status column.</summary>
    public void ApplyReinstallOutcome(ReinstallOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        StatusText = outcome.Title;
        StatusTooltip = outcome.Explanation;
        SuggestedAction = outcome.SuggestedAction;
        IsCriticalReinstallFailure = outcome.IsCritical;
        State = outcome.Kind == ReinstallOutcomeKind.Reinstalled ? UpdateRowState.Updated : UpdateRowState.Failed;
    }

    /// <summary>Applies a <see cref="PersistedUpdateOutcome"/> remembered from a previous session
    /// (see <c>UpdatesViewModel.MergePackages</c>) - reconstructs the same row status/actions
    /// <see cref="ApplyOutcome"/> would have left behind, without re-running anything. Only ever
    /// called for a remembered failure (a success is never persisted - see
    /// <see cref="Winget.UpdateOutcomeMemory"/>), so <see cref="State"/> is always
    /// <see cref="UpdateRowState.Skipped"/> or <see cref="UpdateRowState.Failed"/>.</summary>
    public void ApplyPersistedOutcome(PersistedUpdateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        StatusText = outcome.Title;
        StatusTooltip = outcome.Explanation;
        SuggestedAction = outcome.SuggestedAction;
        IsCriticalReinstallFailure = outcome.IsCriticalReinstallFailure;
        IsPendingVersionConfirmation = false;
        State = outcome.Kind == WingetOutcomeKind.NoApplicableUpdate ? UpdateRowState.Skipped : UpdateRowState.Failed;
    }

    /// <summary>Applies a <see cref="PersistedUpdateOutcome"/> remembered for the "already updated,
    /// but the installed version was unknown so Porchlight can't confirm it" case - see
    /// <see cref="IsPendingVersionConfirmation"/>. Unlike <see cref="ApplyPersistedOutcome"/>, this
    /// is a remembered success, not a failure: the row is shown as updated but hidden by default.</summary>
    public void ApplyPersistedAlreadyUpdatedMarker(PersistedUpdateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        StatusText = outcome.Title;
        StatusTooltip = outcome.Explanation;
        SuggestedAction = WingetSuggestedAction.None;
        IsCriticalReinstallFailure = false;
        IsPendingVersionConfirmation = true;
        IsSelected = false;
        State = UpdateRowState.Updated;
    }

    /// <summary>Applies the result of an install-only retry (see
    /// <see cref="IsCriticalReinstallFailure"/>'s "Try install again" - never re-runs the uninstall
    /// step) to this row's Status column.</summary>
    public void ApplyInstallOnlyOutcome(WingetOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded)
        {
            ApplyOutcome(outcome);
            return;
        }

        StatusText = "Not installed - the new version didn't install";
        StatusTooltip = $"The new version still couldn't be installed. {outcome.Explanation} (winget code {outcome.ExitCodeHex})";
        SuggestedAction = WingetSuggestedAction.Retry;
        IsCriticalReinstallFailure = true;
        State = UpdateRowState.Failed;
    }
}
