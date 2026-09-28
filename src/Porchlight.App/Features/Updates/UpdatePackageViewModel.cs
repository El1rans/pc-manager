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

    /// <summary>"Ignored" / "Pinned / explicit only" / "Current version unknown", in that priority
    /// order - see <c>docs/specs/02-updates.md</c>.</summary>
    public string Notes
    {
        get
        {
            if (IsIgnored)
            {
                return "Ignored";
            }

            if (RequiresExplicit)
            {
                return "Pinned / explicit only";
            }

            if (InstalledVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "Current version unknown";
            }

            return string.Empty;
        }
    }

    public string StatusGlyph => State switch
    {
        UpdateRowState.Updated => "", // checkmark
        UpdateRowState.Failed => "", // error
        UpdateRowState.Updating => "", // sync/progress
        UpdateRowState.Skipped => "", // warning
        UpdateRowState.Queued => "", // clock
        _ => string.Empty,
    };

    /// <summary>"Reinstall..." row action - see <see cref="WingetSuggestedAction.Reinstall"/>.</summary>
    public bool CanReinstall => SuggestedAction == WingetSuggestedAction.Reinstall;

    /// <summary>"Hide this update" row action - see <see cref="WingetSuggestedAction.Hide"/>.</summary>
    public bool CanHide => SuggestedAction == WingetSuggestedAction.Hide;

    /// <summary>"Try again" / "Try install again" row action - see
    /// <see cref="WingetSuggestedAction.Retry"/> and <see cref="WingetSuggestedAction.CloseAppAndRetry"/>.</summary>
    public bool CanRetry => SuggestedAction is WingetSuggestedAction.Retry or WingetSuggestedAction.CloseAppAndRetry;

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
