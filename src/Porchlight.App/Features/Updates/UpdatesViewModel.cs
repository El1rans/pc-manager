using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.Changes;
using Porchlight.Core.Processes;
using Porchlight.Core.Settings;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

/// <summary>View model for the Updates page: lists <c>winget upgrade</c> results, lets the user
/// pick which to install, and runs them one at a time. Every outcome shown is a plain-language
/// <see cref="WingetOutcome"/> (see <c>docs/specs/09-friendly-update-outcomes.md</c>), never a bare
/// hex code. See also <c>docs/specs/02-updates.md</c>.</summary>
public sealed partial class UpdatesViewModel : PageViewModelBase, IDisposable, IBusyGuard
{
    /// <summary>Log panel cap - see <c>docs/specs/02-updates.md</c>.</summary>
    private const int LogCharacterLimit = 200_000;

    /// <summary>Once <see cref="LogCharacterLimit"/> is exceeded, trim back down to roughly this
    /// many characters (at the next line boundary at or after this point) rather than trimming to
    /// the limit itself, so trimming happens in occasional chunks instead of on every single line.</summary>
    private const int LogTrimTarget = 150_000;

    /// <summary>How often buffered log lines are flushed into <see cref="LogText"/> - batching
    /// avoids rebuilding the whole (potentially large) log string on every single line.</summary>
    private static readonly TimeSpan LogFlushInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long a single package's winget operation can run with no result before the Updates page
    /// shows the "may be waiting for you" hint - see <see cref="WaitWithHintAsync"/>. Observed
    /// on the maintainer's PC: Google.CloudSDK's installer opened its own interactive window and sat
    /// there for several minutes with no winget output at all - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>.
    /// </summary>
    public static readonly TimeSpan WaitingHintThreshold = TimeSpan.FromMinutes(2);

    /// <summary>Shown past <see cref="WaitingHintThreshold"/> when <see cref="Silent"/> is off - an
    /// installer with no <c>--silent</c> flag can (and, per the maintainer's real
    /// Google.CloudSDK/OhMyPosh runs, sometimes does) open its own interactive window that just sits
    /// there until someone clicks through it.</summary>
    public const string WaitingHintTextInteractive =
        "Still working. The installer may be waiting for you - look for a setup window on your screen or taskbar.";

    /// <summary>Shown past <see cref="WaitingHintThreshold"/> when <see cref="Silent"/> is on - there
    /// is no window to wait on (a silent install can't prompt), so this stays calm rather than
    /// suggesting the user go look for one. The maintainer's real Google.CloudSDK silent install
    /// simply took about 16 minutes and then succeeded.</summary>
    public const string WaitingHintTextSilent = "Still installing. Large apps can take several minutes.";

    /// <summary>"Waiting for your permission..." - shown the moment winget's own output says it is
    /// about to raise a UAC admin prompt (see <see cref="WingetExitCodes.MentionsAdminPromptRequest"/>),
    /// rather than waiting for <see cref="WaitingHintThreshold"/> to elapse in silence. A UAC prompt
    /// raised from winget's background process can appear only as a flashing taskbar icon, never
    /// brought to the foreground - observed on the maintainer's PC for Google.CloudSDK. See
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    public const string AdminPromptWaitingHintText =
        "Waiting for your permission - look for the Windows prompt on the taskbar.";

    private readonly IChangeJournal? _changeJournal;
    private readonly IAutoRestorePoint? _autoRestorePoint;
    private readonly IWingetClient _wingetClient;
    private readonly ReinstallWorkflow _reinstallWorkflow;
    private readonly ISettingsStore _settingsStore;
    private readonly IUpdateHistoryStore _historyStore;
    private readonly IAppInUseDiagnosticsService _appInUseDiagnostics;
    private readonly IPendingUpdatesTracker _pendingUpdatesTracker;
    private readonly IFileDialogService _fileDialogs;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly StringBuilder _log = new();
    private readonly List<string> _pendingLogLines = [];
    private readonly DispatcherTimer _logFlushTimer;
    private readonly Lock _initialCheckLock = new();

    private CancellationTokenSource _refreshCts = new();
    private bool _disposed;
    private Task? _initialCheckTask;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestReinstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmReinstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryRowCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopAfterCurrentCommand))]
    private bool _isUpdating;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopAfterCurrentCommand))]
    private bool _isStopRequested;

    /// <summary>The current step's description - "Checking for updates...", "[2/5] Updating
    /// Some.Id", "Stopping after the current app finishes...", or the final "Finished: ..." summary.
    /// Shown next to <see cref="ProgressLine"/> rather than combined with it - see
    /// <c>docs/specs/02-updates.md</c>'s "progress row (indeterminate bar + current step + latest
    /// winget progress line)".</summary>
    [ObservableProperty]
    private string? _currentStep;

    /// <summary>The latest raw progress/status text winget itself reported (e.g. a download
    /// percentage) for whichever package is currently being checked or updated.</summary>
    [ObservableProperty]
    private string? _progressLine;

    [ObservableProperty]
    private string _summaryText = "Checking for updates...";

    [ObservableProperty]
    private string? _filterText;

    [ObservableProperty]
    private bool _showIgnored;

    [ObservableProperty]
    private bool _silent;

    [ObservableProperty]
    private bool _includeUnknown;

    [ObservableProperty]
    private bool _checkOnStartup;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _isLogExpanded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedButtonText))]
    private int _selectedCount;

    /// <summary>The DataGrid's currently selected row, for the details panel below it - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s row actions.</summary>
    [ObservableProperty]
    private UpdatePackageViewModel? _selectedRow;

    /// <summary>The row a "Reinstall..." click is asking to confirm, or null when no confirmation
    /// is pending - drives <see cref="IsReinstallConfirmationVisible"/> and
    /// <see cref="ReinstallConfirmationMessage"/>. Kept separate from <see cref="SelectedRow"/> so
    /// the confirmation survives the user clicking elsewhere in the grid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReinstallConfirmationVisible))]
    [NotifyPropertyChangedFor(nameof(ReinstallConfirmationMessage))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmReinstallCommand))]
    private UpdatePackageViewModel? _pendingReinstallRow;

    /// <summary>Set after "Hide this update" - a one-line confirmation ("WinRAR won't be shown
    /// again. You can bring it back with 'Show ignored'.") shown until dismissed or replaced.</summary>
    [ObservableProperty]
    private string? _hideConfirmationMessage;

    /// <summary>"Update selected (N)" - a plain computed property (rather than an inline XAML
    /// <c>StringFormat</c>) so the exact button text is simple to assert on directly.</summary>
    public string UpdateSelectedButtonText =>
        $"Update selected ({SelectedCount.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>True while a "Reinstall..." click is waiting for the user to confirm or cancel -
    /// see <c>docs/specs/09-friendly-update-outcomes.md</c>: "Reinstall requires confirmation".</summary>
    public bool IsReinstallConfirmationVisible => PendingReinstallRow is not null;

    /// <summary>"Reinstall &lt;App&gt;? Porchlight will uninstall &lt;App&gt; and then install the
    /// newest version. ..." - see <c>docs/specs/09-friendly-update-outcomes.md</c> for the exact wording.</summary>
    public string ReinstallConfirmationMessage
    {
        get
        {
            if (PendingReinstallRow is not { } row)
            {
                return string.Empty;
            }

            return $"Reinstall {row.Name}? Porchlight will uninstall {row.Name} and then install the newest " +
                $"version. Your settings for {row.Name} are usually kept, but this can't be guaranteed. " +
                $"Close {row.Name} before continuing.";
        }
    }

    /// <inheritdoc/>
    public bool IsBusyWithWork => IsUpdating || IsMovingApps;

    /// <inheritdoc/>
    public string BusyMessage =>
        "An update is still running. If you close Porchlight now, the current installer keeps " +
        "running but you won't see the result. Close anyway?";

    public UpdatesViewModel(
        IWingetClient wingetClient, ISettingsStore settingsStore, IAppInUseDiagnosticsService appInUseDiagnostics,
        IPendingUpdatesTracker pendingUpdatesTracker, IFileDialogService fileDialogs,
        PorchlightUpdateViewModel porchlightUpdate, IUpdateHistoryStore historyStore,
        IConfirmationDialog confirmation, ILogger<UpdatesViewModel> logger,
        IChangeJournal? changeJournal = null, IAutoRestorePoint? autoRestorePoint = null)
        : this(wingetClient, settingsStore, appInUseDiagnostics, pendingUpdatesTracker, fileDialogs, porchlightUpdate,
            historyStore, confirmation, logger, TimeProvider.System, changeJournal, autoRestorePoint)
    {
    }

    /// <summary>Test seam: lets tests use a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>
    /// instead of a real clock/timer, so the "may be waiting for you" hint's timing
    /// (<see cref="WaitingHintThreshold"/>) never depends on wall-clock timing.</summary>
    public UpdatesViewModel(
        IWingetClient wingetClient, ISettingsStore settingsStore, IAppInUseDiagnosticsService appInUseDiagnostics,
        IPendingUpdatesTracker pendingUpdatesTracker, IFileDialogService fileDialogs,
        PorchlightUpdateViewModel porchlightUpdate, IUpdateHistoryStore historyStore,
        IConfirmationDialog confirmation, ILogger<UpdatesViewModel> logger, TimeProvider timeProvider,
        IChangeJournal? changeJournal = null, IAutoRestorePoint? autoRestorePoint = null)
    {
        _changeJournal = changeJournal;
        _autoRestorePoint = autoRestorePoint;
        _historyStore = historyStore;
        History = new UpdateHistoryViewModel(historyStore, confirmation, timeProvider);
        _pendingUpdatesTracker = pendingUpdatesTracker;
        PorchlightUpdate = porchlightUpdate;
        PorchlightUpdate.PropertyChanged += OnPorchlightUpdateChanged;
        _fileDialogs = fileDialogs;
        _wingetClient = wingetClient;
        _reinstallWorkflow = new ReinstallWorkflow(wingetClient);
        _settingsStore = settingsStore;
        _appInUseDiagnostics = appInUseDiagnostics;
        _logger = logger;
        _timeProvider = timeProvider;

        // Bypasses the On*Changed hooks below (direct field access, not the generated property
        // setter) - loading saved settings must not immediately persist them again or kick off an
        // extra refresh.
        var updates = _settingsStore.Current.Updates;
        _silent = updates.Silent;
        _includeUnknown = updates.IncludeUnknown;
        _checkOnStartup = updates.CheckOnStartup;

        Packages = [];
        Packages.CollectionChanged += OnPackagesCollectionChanged;
        PackagesView = CollectionViewSource.GetDefaultView(Packages);
        PackagesView.Filter = FilterPackage;

        // Falls back to the constructing thread's dispatcher when there is no WPF Application (e.g.
        // unit tests) - harmless there, since it just never ticks; tests instead see up-to-date
        // log text via the explicit FlushLog() calls at the end of each operation below.
        _logFlushTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LogFlushInterval };
        _logFlushTimer.Tick += (_, _) => FlushLog();
        _logFlushTimer.Start();
    }

    public override string Title => "Updates";

    public override string Glyph => "";

    public override int Order => 1;

    public override PageCategory Category => PageCategory.TuneUp;

    /// <summary>The "Porchlight X.Y.Z is available" card - Porchlight's own update, separate from the
    /// winget list below. See <c>docs/specs/23-self-update.md</c>.</summary>
    public PorchlightUpdateViewModel PorchlightUpdate { get; }

    /// <summary>The History body (<c>docs/specs/26-update-history.md</c>).</summary>
    public UpdateHistoryViewModel History { get; }

    /// <summary>True while the page body shows the history list instead of the updates list.</summary>
    [ObservableProperty]
    private bool _isShowingHistory;

    /// <summary>Switches the page body to the history list (reloading it). Safe mid-run: read-only.</summary>
    [RelayCommand]
    private void ShowHistory()
    {
        History.Load();
        IsShowingHistory = true;
    }

    [RelayCommand]
    private void HideHistory() => IsShowingHistory = false;

    public ObservableCollection<UpdatePackageViewModel> Packages { get; }

    public ICollectionView PackagesView { get; }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Navigating away and back returns to the updates list, not the history.
        IsShowingHistory = false;
        return EnsureInitialCheckStartedAsync();
    }

    /// <summary>
    /// Starts the very first check, the first time anything asks for it (either the app-startup
    /// hosted service, or navigating to this page) - see
    /// <c>UpdatesAutoCheckHostedService</c>. Every later call just returns the already-completed
    /// task, so navigating back to this page never re-triggers a check (the spec calls for a check
    /// "when the app starts... and on Refresh", not on every navigation).
    /// </summary>
    internal Task EnsureInitialCheckStartedAsync()
    {
        lock (_initialCheckLock)
        {
            return _initialCheckTask ??= RefreshAsync(quiet: false);
        }
    }

    /// <summary>
    /// Runs <c>winget upgrade</c> and merges the result into <see cref="Packages"/>.
    /// </summary>
    /// <param name="quiet">True for the automatic re-check after an update run finishes: existing
    /// rows keep their last status/state (a row that no longer appears means it updated
    /// successfully) instead of resetting, and the "Checking for updates..." step message is not
    /// shown.</param>
    public async Task RefreshAsync(bool quiet) => await RunRefreshAsync(quiet).ConfigureAwait(true);

    /// <summary>
    /// For the scheduled/tray update check: runs a full (non-quiet) check unless one is already
    /// running or an update run is in progress, and reports what happened as a typed
    /// <see cref="UpdateCheckResult"/> instead of the caller inferring it from status text.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (IsBusy || IsUpdating)
        {
            return UpdateCheckResult.Skipped;
        }

        return await RunRefreshAsync(quiet: false).ConfigureAwait(true);
    }

    private async Task<UpdateCheckResult> RunRefreshAsync(bool quiet)
    {
        var result = UpdateCheckResult.Failed;
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var cancellationToken = cts.Token;

        IsBusy = true;
        if (!quiet)
        {
            CurrentStep = "Checking for updates...";
        }

        try
        {
            // Only a non-quiet refresh reports progress - a quiet re-check happens right after an
            // update run's own "Finished: ..." summary is set as the CurrentStep, and a stray
            // listing-progress line here must not appear to overwrite it.
            IProgress<string>? progress = quiet ? null : new Progress<string>(text => ProgressLine = text);
            var packages = await _wingetClient.GetUpgradesAsync(IncludeUnknown, progress, cancellationToken)
                .ConfigureAwait(true);
            MergePackages(packages, quiet);
            result = UpdateCheckResult.Succeeded(Packages.Count(p => !p.IsHiddenByDefault));
        }
        catch (OperationCanceledException)
        {
            result = UpdateCheckResult.Skipped;
            // Superseded by a newer refresh (or the app is shutting down); expected, not an error.
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
            SummaryText = ex.Message;
            FlushLog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check for updates.");
            AppendLog("ERROR: " + ex.Message);
            SummaryText = "Could not check for updates.";
            FlushLog();
        }
        finally
        {
            // Only this call's own token still being the current one means no newer RefreshAsync
            // (or this same one racing with itself) has already taken over - an older, superseded
            // call reaching here (e.g. its GetUpgradesAsync happened to return before it noticed
            // cancellation) must not clear IsBusy out from under the newer one that is still running.
            if (ReferenceEquals(_refreshCts, cts))
            {
                IsBusy = false;
                ProgressLine = null;
                if (!quiet)
                {
                    CurrentStep = null;
                }
            }
        }

        return result;
    }

    private void MergePackages(IReadOnlyList<WingetPackage> packages, bool quiet)
    {
        var ignoredIds = new HashSet<string>(_settingsStore.Current.Updates.IgnoredIds, StringComparer.OrdinalIgnoreCase);
        var previous = Packages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var lastOutcomes = _settingsStore.Current.Updates.LastOutcomes;

        Packages.Clear();
        foreach (var package in packages)
        {
            var isIgnored = ignoredIds.Contains(package.Id);
            var row = new UpdatePackageViewModel(package) { IsIgnored = isIgnored };

            if (previous.TryGetValue(package.Id, out var oldRow))
            {
                // A row that just finished successfully never reappears here (it no longer has an
                // upgrade available). One still present (still needs an update, was skipped, or
                // failed - including one whose installed version is "Unknown" and so can look like
                // a "new" package every check) keeps its selection unless it already finished.
                row.IsSelected = oldRow.IsSelected && oldRow.State != UpdateRowState.Updated;
                if (quiet)
                {
                    row.State = oldRow.State;
                    row.StatusText = oldRow.StatusText;
                    row.StatusTooltip = oldRow.StatusTooltip;
                    row.SuggestedAction = oldRow.SuggestedAction;
                    row.IsCriticalReinstallFailure = oldRow.IsCriticalReinstallFailure;
                    row.IsPendingVersionConfirmation = oldRow.IsPendingVersionConfirmation;
                }
            }
            else
            {
                row.IsSelected = !isIgnored && !package.RequiresExplicit;
            }

            // Whatever was remembered for this exact id + available version (see
            // UpdateOutcomeMemory.Find) always wins over the in-session carryover above - not just
            // "no previous row" (a brand-new view model after an app restart), but every refresh:
            // e.g. right after PersistUpgradeOutcome remembers an "Unknown" installed-version
            // package as "already updated", the very next (quiet) refresh must hide it immediately
            // rather than only starting from the next app session. See
            // docs/specs/09-friendly-update-outcomes.md's addendum.
            var remembered = UpdateOutcomeMemory.Find(lastOutcomes, package.Id, package.AvailableVersion);
            if (remembered is { Kind: WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded })
            {
                row.ApplyPersistedAlreadyUpdatedMarker(remembered);
            }
            else if (remembered is not null)
            {
                row.ApplyPersistedOutcome(remembered);
            }

            Packages.Add(row);
        }

        // Bound the remembered-outcomes list to whatever winget currently lists, rather than
        // letting it grow forever - see UpdateOutcomeMemory.Prune.
        var currentIds = packages.Select(p => p.Id).ToList();
        _settingsStore.Update(s => UpdateOutcomeMemory.Prune(s.Updates.LastOutcomes, currentIds));

        UpdateSummary();
        UpdateBadge();
        _pendingUpdatesTracker.Report(
            Packages.Where(p => !p.IsHiddenByDefault)
                .Select(p => new PendingUpdate(p.Name, p.InstalledVersion, p.AvailableVersion))
                .ToList(),
            _timeProvider.GetLocalNow());
    }

    private void UpdateSummary()
    {
        var visible = Packages.Count(p => !p.IsHiddenByDefault);
        var ignored = Packages.Count(p => p.IsHiddenByDefault);

        var text = visible == 1 ? "1 update available" : $"{visible} updates available";
        if (ignored > 0)
        {
            text += ignored == 1 ? " (1 ignored)" : $" ({ignored} ignored)";
        }

        text += " - last checked " + DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        SummaryText = text;
    }

    private void OnPorchlightUpdateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PorchlightUpdateViewModel.IsUpdateAvailable))
        {
            UpdateBadge();
        }
    }

    /// <summary>The nav badge counts winget updates plus one for a newer Porchlight, if there is one.</summary>
    private void UpdateBadge()
    {
        var count = Packages.Count(p => !p.IsHiddenByDefault) + (PorchlightUpdate.IsUpdateAvailable ? 1 : 0);
        Badge = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : null;
    }

    private bool FilterPackage(object item)
    {
        if (item is not UpdatePackageViewModel row)
        {
            return false;
        }

        if (row.IsHiddenByDefault && !ShowIgnored)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        return row.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
            row.Id.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnFilterTextChanged(string? value) => PackagesView.Refresh();

    partial void OnShowIgnoredChanged(bool value) => PackagesView.Refresh();

    partial void OnSilentChanged(bool value) => _settingsStore.Update(s => s.Updates.Silent = value);

    partial void OnIncludeUnknownChanged(bool value)
    {
        _settingsStore.Update(s => s.Updates.IncludeUnknown = value);

        // The checkbox is disabled in the view while IsBusy (which covers IsUpdating too), but
        // guard here as well in case this is ever set programmatically: never contend with an
        // in-flight update run for ownership of IsBusy/Packages - see RefreshAsync's finally block.
        if (IsUpdating)
        {
            return;
        }

        _ = RefreshAsync(quiet: false);
    }

    partial void OnCheckOnStartupChanged(bool value) => _settingsStore.Update(s => s.Updates.CheckOnStartup = value);

    // The refresh button also re-checks for a new Porchlight version (a failed or offline check is silent).
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task Refresh() => Task.WhenAll(PorchlightUpdate.CheckAsync(), RefreshAsync(quiet: false));

    private bool CanRefresh() => !IsBusy;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Packages.Where(p => !p.IsIgnored))
        {
            row.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var row in Packages)
        {
            row.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
    private async Task UpdateSelectedAsync()
    {
        var selected = Packages.Where(p => p.IsSelected && !p.IsIgnored).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        foreach (var row in selected)
        {
            row.State = UpdateRowState.Queued;
            row.StatusText = "Queued";
        }

        IsStopRequested = false;
        IsBusy = true;
        IsUpdating = true;

        // One counter per WingetOutcomeKind bucket, reported in the plain-language summary below -
        // see docs/specs/09-friendly-update-outcomes.md: "Finished: 1 updated, 1 needs a reinstall,
        // 1 not available for this PC".
        var counts = new Dictionary<WingetOutcomeKind, int>();
        var restartNeeded = 0;
        var cancelledByStop = 0;
        var ignoredMidRun = 0;
        var completed = 0;

        var stepProgress = new Progress<string>(text => ProgressLine = text);

        await EnsureRestorePointAsync(selected.Count).ConfigureAwait(true);

        foreach (var row in selected)
        {
            // Ignoring a row (from the context menu) while it is still queued must actually skip
            // it - the selection above was captured once, at the start of the run, so without this
            // check an ignore mid-run would have no effect on a row not yet reached.
            if (row.IsIgnored)
            {
                row.State = UpdateRowState.Skipped;
                row.StatusText = "Ignored";
                ignoredMidRun++;
                continue;
            }

            if (IsStopRequested)
            {
                row.State = UpdateRowState.Skipped;
                row.StatusText = "Cancelled";
                cancelledByStop++;
                continue;
            }

            completed++;
            row.State = UpdateRowState.Updating;
            row.StatusText = "Updating...";
            CurrentStep = $"[{completed}/{selected.Count}] Updating {row.Id}";

            var logProgress = CreateLogProgress(row);
            try
            {
                // Always CancellationToken.None: once winget has actually launched, killing it
                // mid-install can leave the package half-installed. "Stop after current" is the
                // IsStopRequested flag checked above, only honoured between packages - see
                // IWingetClient.UpgradeAsync.
                var operationTask = _wingetClient
                    .UpgradeAsync(row.Id, Silent, logProgress, stepProgress, CancellationToken.None);
                await WaitWithHintAsync(row, operationTask).ConfigureAwait(true);
                var result = await operationTask.ConfigureAwait(true);

                var outcome = WingetExitCodes.DescribeOutcome(result.ExitCode, result.Lines);
                outcome = await EnrichAppInUseOutcomeAsync(row, outcome).ConfigureAwait(true);
                row.ApplyOutcome(outcome);
                PersistUpgradeOutcome(row, outcome);
                counts[outcome.Kind] = counts.GetValueOrDefault(outcome.Kind) + 1;
                if (outcome.Kind == WingetOutcomeKind.UpdatedRestartNeeded)
                {
                    restartNeeded++;
                }

                AppendLog($"<< {row.Id}: {outcome.Title} (exit {outcome.ExitCodeHex})");
                AppendLog(string.Empty);
            }
            catch (WingetNotFoundException ex)
            {
                row.State = UpdateRowState.Failed;
                row.StatusText = "Something went wrong";
                counts[WingetOutcomeKind.Failed] = counts.GetValueOrDefault(WingetOutcomeKind.Failed) + 1;
                AppendLog("ERROR: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure updating {PackageId}.", row.Id);
                row.State = UpdateRowState.Failed;
                row.StatusText = "Something went wrong";
                counts[WingetOutcomeKind.Failed] = counts.GetValueOrDefault(WingetOutcomeKind.Failed) + 1;
                AppendLog("ERROR: " + ex.Message);
            }
            // Flushed after every package (rather than left to the ~100ms timer alone) so the log
            // never looks stale for longer than one package's worth of output, and so tests never
            // need to wait on the real timer to see a completed package's log lines.
            FlushLog();
        }

        var summary = BuildFinishedSummary(counts, restartNeeded, cancelledByStop, ignoredMidRun);
        CurrentStep = summary;
        ProgressLine = null;
        AppendLog(summary);
        FlushLog();
        PlayFinishedSound();

        IsUpdating = false;
        IsBusy = false;

        await RefreshAsync(quiet: true).ConfigureAwait(true);
    }

    /// <summary>Builds "Finished: 1 updated (1 needs a restart), 1 needs a reinstall, 1 not
    /// available for this PC, ..." - one clause per non-zero outcome bucket, in a fixed, readable
    /// order, plus "N cancelled" for rows skipped by "Stop after current" - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
    private static string BuildFinishedSummary(
        IReadOnlyDictionary<WingetOutcomeKind, int> counts, int restartNeeded, int cancelledByStop, int ignoredMidRun)
    {
        var updated = counts.GetValueOrDefault(WingetOutcomeKind.Updated) +
            counts.GetValueOrDefault(WingetOutcomeKind.UpdatedRestartNeeded);

        List<string> clauses = [];
        if (updated > 0)
        {
            var restartSuffix = restartNeeded switch
            {
                0 => string.Empty,
                1 => " (1 needs a restart)",
                _ => $" ({restartNeeded.ToString(CultureInfo.InvariantCulture)} need a restart)",
            };
            clauses.Add(Pluralize(updated, "updated", "updated") + restartSuffix);
        }

        AddClause(clauses, counts, WingetOutcomeKind.ReinstallRequired, "needs a reinstall", "need a reinstall");
        AddClause(clauses, counts, WingetOutcomeKind.NoApplicableUpdate, "not available for this PC", "not available for this PC");
        AddClause(clauses, counts, WingetOutcomeKind.AppRunning, "needs the app closed", "need the app closed");
        AddClause(clauses, counts, WingetOutcomeKind.Cancelled, "cancelled", "cancelled");
        AddClause(clauses, counts, WingetOutcomeKind.NeedsAdmin, "needs administrator approval", "need administrator approval");
        AddClause(clauses, counts, WingetOutcomeKind.Blocked, "blocked by policy", "blocked by policy");
        AddClause(clauses, counts, WingetOutcomeKind.NetworkProblem, "couldn't download", "couldn't download");
        AddClause(clauses, counts, WingetOutcomeKind.Failed, "failed", "failed");

        if (cancelledByStop > 0)
        {
            clauses.Add(Pluralize(cancelledByStop, "stopped before it started", "stopped before they started"));
        }

        if (ignoredMidRun > 0)
        {
            clauses.Add(Pluralize(ignoredMidRun, "ignored", "ignored"));
        }

        return clauses.Count == 0 ? "Finished: nothing to update" : "Finished: " + string.Join(", ", clauses);
    }

    private static void AddClause(
        List<string> clauses, IReadOnlyDictionary<WingetOutcomeKind, int> counts, WingetOutcomeKind kind,
        string singular, string plural)
    {
        var count = counts.GetValueOrDefault(kind);
        if (count > 0)
        {
            clauses.Add(Pluralize(count, singular, plural));
        }
    }

    private static string Pluralize(int count, string singularSuffix, string pluralSuffix) =>
        count == 1
            ? $"{count.ToString(CultureInfo.InvariantCulture)} {singularSuffix}"
            : $"{count.ToString(CultureInfo.InvariantCulture)} {pluralSuffix}";

    private bool CanUpdateSelected() => !IsBusy && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanStopAfterCurrent))]
    private void StopAfterCurrent()
    {
        IsStopRequested = true;
        CurrentStep = "Stopping after the current app finishes...";
    }

    private bool CanStopAfterCurrent() => IsUpdating && !IsStopRequested;

    /// <summary>Adds the given ids to the ignore list (persisted), unselects and marks those rows
    /// ignored, and refreshes the summary/badge/filter. Called from the view's context menu with
    /// the DataGrid's currently selected rows. If a row is currently queued or in-flight as part of
    /// an update run, ignoring it here still lets the run notice - see the <c>IsIgnored</c> check
    /// in <see cref="UpdateSelectedAsync"/>.</summary>
    public void Ignore(IEnumerable<UpdatePackageViewModel> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        _settingsStore.Update(s =>
        {
            foreach (var row in list)
            {
                if (!s.Updates.IgnoredIds.Contains(row.Id, StringComparer.OrdinalIgnoreCase))
                {
                    s.Updates.IgnoredIds.Add(row.Id);
                }
            }
        });

        foreach (var row in list)
        {
            row.IsIgnored = true;
            row.IsSelected = false;
        }

        PackagesView.Refresh();
        UpdateSummary();
        UpdateBadge();
    }

    /// <summary>Removes the given ids from the ignore list - see <see cref="Ignore"/>.</summary>
    public void StopIgnoring(IEnumerable<UpdatePackageViewModel> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var ids = new HashSet<string>(list.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        _settingsStore.Update(s => s.Updates.IgnoredIds.RemoveAll(id => ids.Contains(id)));

        foreach (var row in list)
        {
            row.IsIgnored = false;
        }

        PackagesView.Refresh();
        UpdateSummary();
        UpdateBadge();
    }

    /// <summary>"Hide this update" row action - ignores the single row and shows a one-line
    /// confirmation. See <c>docs/specs/09-friendly-update-outcomes.md</c>: "'WinRAR won't be shown
    /// again. You can bring it back with 'Show ignored'.'"</summary>
    [RelayCommand(CanExecute = nameof(CanHideRow))]
    private void HideUpdate(UpdatePackageViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var name = row.Name;
        Ignore([row]);
        HideConfirmationMessage = $"{name} won't be shown again. You can bring it back with \"Show ignored\".";
    }

    private static bool CanHideRow(UpdatePackageViewModel? row) => row is not null;

    [RelayCommand]
    private void DismissHideConfirmation() => HideConfirmationMessage = null;

    /// <summary>Starts the "Reinstall..." confirmation for <paramref name="row"/> - see
    /// <see cref="IsReinstallConfirmationVisible"/>. Does nothing yet; the actual uninstall/install
    /// only happens from <see cref="ConfirmReinstallAsync"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanRequestReinstall))]
    private void RequestReinstall(UpdatePackageViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        PendingReinstallRow = row;
    }

    private bool CanRequestReinstall(UpdatePackageViewModel? row) => row is not null && !IsBusy;

    [RelayCommand]
    private void CancelReinstall() => PendingReinstallRow = null;

    /// <summary>Confirms the pending "Reinstall..." request and actually runs
    /// <see cref="ReinstallWorkflow"/> for it.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmReinstall))]
    private async Task ConfirmReinstallAsync()
    {
        var row = PendingReinstallRow;
        PendingReinstallRow = null;
        if (row is null)
        {
            return;
        }

        await RunReinstallAsync(row).ConfigureAwait(true);
    }

    private bool CanConfirmReinstall() => PendingReinstallRow is not null && !IsBusy;

    private async Task RunReinstallAsync(UpdatePackageViewModel row)
    {
        IsBusy = true;
        IsUpdating = true;
        CurrentStep = $"Reinstalling {row.Id}";

        var logProgress = CreateLogProgress(row);
        var stepProgress = new Progress<string>(text => ProgressLine = text);
        var succeeded = false;

        try
        {
            var outcome = await _reinstallWorkflow
                .RunAsync(row.Id, Silent, logProgress, stepProgress, CancellationToken.None)
                .ConfigureAwait(true);

            row.ApplyReinstallOutcome(outcome);
            PersistReinstallOutcome(row, outcome);
            succeeded = outcome.Kind == ReinstallOutcomeKind.Reinstalled;

            var summary = succeeded
                ? $"Finished: {row.Id} reinstalled"
                : $"Finished: {row.Id} - {outcome.Title}";
            CurrentStep = summary;
            AppendLog($"<< {row.Id}: {outcome.Title}");
            AppendLog(summary);
        }
        catch (WingetNotFoundException ex)
        {
            row.State = UpdateRowState.Failed;
            row.StatusText = "Something went wrong";
            AppendLog("ERROR: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure reinstalling {PackageId}.", row.Id);
            row.State = UpdateRowState.Failed;
            row.StatusText = "Something went wrong";
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            FlushLog();
            ProgressLine = null;
            IsUpdating = false;
            IsBusy = false;
        }

        PlayFinishedSound();

        // Only a fully successful reinstall re-checks: the package is expected to drop off the
        // winget upgrade listing once it succeeds. A failed reinstall (especially the critical
        // "uninstalled but the new install failed" state) must not be silently refreshed away just
        // because winget upgrade would no longer list an uninstalled package - the row's critical
        // status has to stay visible until the user retries or manually refreshes.
        if (succeeded)
        {
            await RefreshAsync(quiet: true).ConfigureAwait(true);
        }
    }

    /// <summary>"Try again" / "Try install again" row action - re-runs a plain upgrade for
    /// <see cref="WingetSuggestedAction.Retry"/> / <see cref="WingetSuggestedAction.CloseAppAndRetry"/>,
    /// or an install-only retry when <see cref="UpdatePackageViewModel.IsCriticalReinstallFailure"/>
    /// is set (never re-runs the uninstall step).</summary>
    [RelayCommand(CanExecute = nameof(CanRetryRow))]
    private async Task RetryRowAsync(UpdatePackageViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsCriticalReinstallFailure)
        {
            await RunInstallOnlyRetryAsync(row).ConfigureAwait(true);
        }
        else
        {
            await RunSingleUpgradeAsync(row).ConfigureAwait(true);
        }
    }

    private bool CanRetryRow(UpdatePackageViewModel? row) => row is not null && !IsBusy;

    private async Task RunSingleUpgradeAsync(UpdatePackageViewModel row)
    {
        IsBusy = true;
        IsUpdating = true;
        CurrentStep = $"Updating {row.Id}";
        row.State = UpdateRowState.Updating;
        row.StatusText = "Updating...";

        var logProgress = CreateLogProgress(row);
        var stepProgress = new Progress<string>(text => ProgressLine = text);
        var succeeded = false;

        try
        {
            var operationTask = _wingetClient.UpgradeAsync(row.Id, Silent, logProgress, stepProgress, CancellationToken.None);
            await WaitWithHintAsync(row, operationTask).ConfigureAwait(true);
            var result = await operationTask.ConfigureAwait(true);
            var outcome = WingetExitCodes.DescribeOutcome(result.ExitCode, result.Lines);
            outcome = await EnrichAppInUseOutcomeAsync(row, outcome).ConfigureAwait(true);
            row.ApplyOutcome(outcome);
            PersistUpgradeOutcome(row, outcome);
            succeeded = outcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded;
            CurrentStep = $"Finished: {row.Id} - {outcome.Title}";
            AppendLog($"<< {row.Id}: {outcome.Title} (exit {outcome.ExitCodeHex})");
        }
        catch (WingetNotFoundException ex)
        {
            row.State = UpdateRowState.Failed;
            row.StatusText = "Something went wrong";
            AppendLog("ERROR: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure retrying {PackageId}.", row.Id);
            row.State = UpdateRowState.Failed;
            row.StatusText = "Something went wrong";
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            FlushLog();
            ProgressLine = null;
            IsUpdating = false;
            IsBusy = false;
        }

        if (succeeded)
        {
            await RefreshAsync(quiet: true).ConfigureAwait(true);
        }
    }

    private async Task RunInstallOnlyRetryAsync(UpdatePackageViewModel row)
    {
        IsBusy = true;
        IsUpdating = true;
        CurrentStep = $"Installing {row.Id}";

        var logProgress = CreateLogProgress(row);
        var stepProgress = new Progress<string>(text => ProgressLine = text);
        var succeeded = false;

        try
        {
            var operationTask = _wingetClient.InstallAsync(row.Id, Silent, logProgress, stepProgress, CancellationToken.None);
            await WaitWithHintAsync(row, operationTask).ConfigureAwait(true);
            var result = await operationTask.ConfigureAwait(true);
            var outcome = WingetExitCodes.DescribeOutcome(result.ExitCode, result.Lines);
            row.ApplyInstallOnlyOutcome(outcome);
            PersistInstallOnlyOutcome(row, outcome);
            succeeded = outcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded;
            CurrentStep = $"Finished: {row.Id} - {row.StatusText}";
            AppendLog($"<< {row.Id}: {outcome.Title} (exit {outcome.ExitCodeHex})");
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure installing {PackageId}.", row.Id);
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            FlushLog();
            ProgressLine = null;
            IsUpdating = false;
            IsBusy = false;
        }

        if (succeeded)
        {
            await RefreshAsync(quiet: true).ConfigureAwait(true);
        }
    }

    /// <summary>Wraps <see cref="AppendLog"/> with live detection of winget's "will request to run
    /// as administrator" line (<see cref="WingetExitCodes.MentionsAdminPromptRequest"/>) for
    /// <paramref name="row"/> - sets <see cref="UpdatePackageViewModel.WaitingHint"/> to
    /// <see cref="AdminPromptWaitingHintText"/> the moment it's seen (rather than waiting for
    /// <see cref="WaitingHintThreshold"/> to elapse in silence - see <see cref="WaitWithHintAsync"/>),
    /// and clears it again as soon as any further output line arrives - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum: "Clear it when further output
    /// arrives or the operation ends" (the latter is already handled by
    /// <see cref="WaitWithHintAsync"/>'s <c>finally</c>).</summary>
    private Progress<string> CreateLogProgress(UpdatePackageViewModel row)
    {
        var sawAdminPrompt = false;
        return new Progress<string>(line =>
        {
            AppendLog(line);
            if (WingetExitCodes.MentionsAdminPromptRequest(line))
            {
                sawAdminPrompt = true;
                row.WaitingHint = AdminPromptWaitingHintText;
            }
            else if (sawAdminPrompt)
            {
                sawAdminPrompt = false;
                row.WaitingHint = null;
            }
        });
    }

    /// <summary>For the "app in use" outcome specifically (see
    /// <see cref="Porchlight.Core.Processes.WingetExitCodes.AppInUseByAnotherApplication"/>), tries
    /// to replace the generic "close the app" explanation with one naming the actual programs
    /// holding the app's files open (<see cref="IAppInUseDiagnosticsService"/>) - see
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum: real case, OBS Studio blocked
    /// by Chrome and another app while OBS itself was not running. Falls back to
    /// <paramref name="outcome"/> unchanged if the lookup finds nothing or fails.</summary>
    private async Task<WingetOutcome> EnrichAppInUseOutcomeAsync(UpdatePackageViewModel row, WingetOutcome outcome)
    {
        if (outcome.ExitCode != WingetExitCodes.AppInUseByAnotherApplication)
        {
            return outcome;
        }

        var enriched = await _appInUseDiagnostics
            .TryDescribeLockingProcessesAsync(row.Id, row.Name)
            .ConfigureAwait(true);
        return enriched is null ? outcome : outcome with { Explanation = enriched };
    }

    /// <summary>Remembers/forgets the outcome of a plain upgrade for
    /// <see cref="UpdateOutcomeMemory"/> - see <c>docs/specs/09-friendly-update-outcomes.md</c>'s
    /// "Remember last outcome across restarts" addendum. A package whose <c>InstalledVersion</c> is
    /// "Unknown" is a special case: winget reporting success there doesn't tell Porchlight whether
    /// the old install was actually replaced or a second copy was installed alongside it (observed
    /// for Google.CloudSDK), so that case is remembered as "already updated" rather than forgotten
    /// outright - see <see cref="UpdatePackageViewModel.IsPendingVersionConfirmation"/>.</summary>
    private void PersistUpgradeOutcome(
        UpdatePackageViewModel row, WingetOutcome outcome, UpdateHistoryAction action = UpdateHistoryAction.Update)
    {
        RecordHistory(row, action, IsSuccess(outcome), outcome.Title, outcome.Explanation, outcome.ExitCode);

        if (outcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded)
        {
            if (row.InstalledVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                RememberPersistedOutcome(new PersistedUpdateOutcome
                {
                    PackageId = row.Id,
                    AvailableVersion = row.AvailableVersion,
                    Kind = outcome.Kind,
                    Title = outcome.Title,
                    Explanation = $"Already updated to {row.AvailableVersion}. The previous installed version " +
                        "couldn't be determined, so if this keeps reappearing after installing again, it may " +
                        "have installed a second copy rather than replacing the old one.",
                    ExitCode = outcome.ExitCode,
                    SuggestedAction = WingetSuggestedAction.None,
                    IsCriticalReinstallFailure = false,
                });
                return;
            }

            ForgetPersistedOutcome(row.Id);
            return;
        }

        RememberPersistedOutcome(new PersistedUpdateOutcome
        {
            PackageId = row.Id,
            AvailableVersion = row.AvailableVersion,
            Kind = outcome.Kind,
            Title = outcome.Title,
            Explanation = outcome.TooltipText,
            ExitCode = outcome.ExitCode,
            SuggestedAction = outcome.SuggestedAction,
            IsCriticalReinstallFailure = false,
        });
    }

    /// <summary>Remembers/forgets a <see cref="ReinstallWorkflow"/> run's outcome - see
    /// <see cref="PersistUpgradeOutcome"/>. Reinstall always uninstalls first, so the "Unknown"
    /// second-copy risk that upgrade has does not apply here.</summary>
    private void PersistReinstallOutcome(UpdatePackageViewModel row, ReinstallOutcome outcome)
    {
        RecordHistory(
            row, UpdateHistoryAction.Reinstall, outcome.Kind == ReinstallOutcomeKind.Reinstalled, outcome.Title,
            outcome.Explanation, outcome.InstallOutcome?.ExitCode ?? outcome.UninstallOutcome.ExitCode);

        if (outcome.Kind == ReinstallOutcomeKind.Reinstalled)
        {
            ForgetPersistedOutcome(row.Id);
            return;
        }

        RememberPersistedOutcome(new PersistedUpdateOutcome
        {
            PackageId = row.Id,
            AvailableVersion = row.AvailableVersion,
            Kind = WingetOutcomeKind.Failed,
            Title = outcome.Title,
            Explanation = outcome.Explanation,
            ExitCode = outcome.InstallOutcome?.ExitCode ?? outcome.UninstallOutcome.ExitCode,
            SuggestedAction = outcome.SuggestedAction,
            IsCriticalReinstallFailure = outcome.IsCritical,
        });
    }

    /// <summary>Remembers/forgets an install-only retry's outcome (see
    /// <see cref="UpdatePackageViewModel.IsCriticalReinstallFailure"/>) - see
    /// <see cref="PersistUpgradeOutcome"/>. Delegates the success case to
    /// <see cref="PersistUpgradeOutcome"/> since <see cref="UpdatePackageViewModel.ApplyInstallOnlyOutcome"/>
    /// treats it identically to a plain successful upgrade.</summary>
    private void PersistInstallOnlyOutcome(UpdatePackageViewModel row, WingetOutcome outcome)
    {
        if (IsSuccess(outcome))
        {
            PersistUpgradeOutcome(row, outcome, UpdateHistoryAction.Install);
            return;
        }

        RecordHistory(row, UpdateHistoryAction.Install, false, outcome.Title, outcome.Explanation, outcome.ExitCode);
        RememberPersistedOutcome(new PersistedUpdateOutcome
        {
            PackageId = row.Id,
            AvailableVersion = row.AvailableVersion,
            Kind = WingetOutcomeKind.Failed,
            Title = "Not installed - the new version didn't install",
            Explanation = $"The new version still couldn't be installed. {outcome.Explanation} (winget code {outcome.ExitCodeHex})",
            ExitCode = outcome.ExitCode,
            SuggestedAction = WingetSuggestedAction.Retry,
            IsCriticalReinstallFailure = true,
        });
    }

    /// <summary>Before a batch of updates, asks for a restore point when the setting is on and Windows
    /// allows it. Never blocks the batch; the outcome is only noted in the log.</summary>
    private async Task EnsureRestorePointAsync(int packageCount)
    {
        if (_autoRestorePoint is null)
        {
            return;
        }

        CurrentStep = "Checking for a restore point first...";
        var result = await _autoRestorePoint
            .EnsureAsync($"Porchlight: update {packageCount.ToString(CultureInfo.InvariantCulture)} apps", CancellationToken.None)
            .ConfigureAwait(true);
        if (result.Note is not null)
        {
            AppendLog(result.Note);
        }
    }

    private void RecordChange(UpdatePackageViewModel row, UpdateHistoryAction action)
    {
        var verb = action switch
        {
            UpdateHistoryAction.Install => "Installed",
            UpdateHistoryAction.Reinstall => "Reinstalled",
            _ => "Updated",
        };
        var version = string.IsNullOrWhiteSpace(row.AvailableVersion) ? string.Empty : $" to {row.AvailableVersion}";
        _changeJournal?.Record(ChangeArea.Updates, $"{verb} {row.Name}{version}");
    }

    private static bool IsSuccess(WingetOutcome outcome) =>
        outcome.Kind is WingetOutcomeKind.Updated or WingetOutcomeKind.UpdatedRestartNeeded;

    /// <summary>Adds one entry to the update history (<c>docs/specs/26-update-history.md</c>). Never
    /// throws: a history problem must not break an update run.</summary>
    private void RecordHistory(
        UpdatePackageViewModel row, UpdateHistoryAction action, bool succeeded, string title, string explanation,
        int exitCode)
    {
        try
        {
            var installed = row.InstalledVersion;
            _historyStore.Add(new UpdateHistoryEntry
            {
                TimestampUtc = _timeProvider.GetUtcNow(),
                PackageId = row.Id,
                PackageName = row.Name,
                FromVersion = string.IsNullOrWhiteSpace(installed) || installed.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : installed,
                ToVersion = string.IsNullOrWhiteSpace(row.AvailableVersion) ? null : row.AvailableVersion,
                Action = action,
                Succeeded = succeeded,
                OutcomeTitle = title,
                Explanation = succeeded ? string.Empty : explanation,
                ExitCode = exitCode,
            });

            if (IsShowingHistory)
            {
                History.Load();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't record {PackageId} in the update history.", row.Id);
        }

        if (succeeded)
        {
            RecordChange(row, action);
        }
    }

    private void RememberPersistedOutcome(PersistedUpdateOutcome outcome) =>
        _settingsStore.Update(s => UpdateOutcomeMemory.Remember(s.Updates.LastOutcomes, outcome));

    private void ForgetPersistedOutcome(string packageId) =>
        _settingsStore.Update(s => UpdateOutcomeMemory.Forget(s.Updates.LastOutcomes, packageId));

    /// <summary>
    /// Waits for <paramref name="operationTask"/> (a winget upgrade/install already in flight for
    /// <paramref name="row"/>), and if it is still running after <see cref="WaitingHintThreshold"/>,
    /// sets <see cref="UpdatePackageViewModel.WaitingHint"/> (and mirrors it into
    /// <see cref="ProgressLine"/>) - <see cref="WaitingHintTextInteractive"/> normally, or
    /// <see cref="WaitingHintTextSilent"/> when <see cref="Silent"/> is on (a silent install has no
    /// window to wait on). Cleared again once the operation finishes. Uses
    /// <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> against
    /// <see cref="_timeProvider"/> (same pattern as <c>RemoteSupportViewModel</c>'s polling) rather
    /// than a raw timer, so it resumes on the captured UI thread automatically and a test can drive
    /// it deterministically with a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>.
    /// Never throws <paramref name="operationTask"/>'s own exception - the caller always awaits that
    /// task itself right after this method returns and handles success/failure there.
    /// </summary>
    private async Task WaitWithHintAsync(UpdatePackageViewModel row, Task operationTask)
    {
        var delayTask = Task.Delay(WaitingHintThreshold, _timeProvider, CancellationToken.None);
        try
        {
            var first = await Task.WhenAny(operationTask, delayTask).ConfigureAwait(true);
            if (ReferenceEquals(first, delayTask))
            {
                // Don't stomp on a more specific hint already showing - see CreateLogProgress:
                // winget's own output saying it's about to raise a UAC prompt is detected the
                // moment it's seen, independent of this threshold, and is more useful than the
                // generic text below.
                if (row.WaitingHint is null)
                {
                    var hintText = Silent ? WaitingHintTextSilent : WaitingHintTextInteractive;
                    row.WaitingHint = hintText;
                    ProgressLine = hintText;
                }

                try
                {
                    await operationTask.ConfigureAwait(true);
                }
                catch
                {
                    // Ignored - see this method's remarks; the caller re-awaits operationTask.
                }
            }
        }
        finally
        {
            row.WaitingHint = null;
        }
    }

    /// <summary>Runs <c>winget show</c> for one package into the Log panel (expanding it first) -
    /// the "Show package info" context menu item. Disabled in the view while an update is running
    /// (<see cref="IsUpdating"/>) so it never interleaves with a running upgrade's own log lines.</summary>
    public async Task ShowPackageInfoAsync(UpdatePackageViewModel row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        IsLogExpanded = true;

        try
        {
            var log = new Progress<string>(AppendLog);
            await _wingetClient.ShowAsync(row.Id, log, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigating away while the lookup was in flight; expected, not an error.
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not show package info for {PackageId}.", row.Id);
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            FlushLog();
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var path = AppDataPaths.LogsDirectory;
        try
        {
            Directory.CreateDirectory(path);
            // Absolute path: never let a same-named exe in the working directory run elevated.
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer, path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Could not open the log folder at {Path}.", path);
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        _pendingLogLines.Clear();
        _log.Clear();
        LogText = string.Empty;
    }

    /// <summary>Queues one line for the Log panel and mirrors it immediately to the app log - see
    /// <c>docs/specs/02-updates.md</c>: "Everything written to the Log panel also goes to the app
    /// log." <see cref="LogText"/> itself is only rebuilt when <see cref="FlushLog"/> runs (every
    /// <see cref="LogFlushInterval"/>, or explicitly at the end of an operation), not on every call
    /// to this method - rebuilding the whole log string on every single line would be O(n) per line
    /// (O(n<sup>2</sup>) overall for a run with many lines). Called only from the UI thread (every
    /// caller is a <see cref="Progress{T}"/> callback created on it, or code already running on
    /// it), so the pending-lines buffer needs no locking.</summary>
    private void AppendLog(string line)
    {
        LogAppendedLine(line);
        _pendingLogLines.Add(line);
    }

    /// <summary>Moves every pending line (see <see cref="AppendLog"/>) into <see cref="_log"/>,
    /// trims it back down to <see cref="LogTrimTarget"/> characters (at a line boundary) if it grew
    /// past <see cref="LogCharacterLimit"/>, and republishes <see cref="LogText"/>. A no-op if
    /// nothing is pending, so the ~100ms timer tick is cheap between bursts of log activity.</summary>
    private void FlushLog()
    {
        if (_pendingLogLines.Count == 0)
        {
            return;
        }

        foreach (var line in _pendingLogLines)
        {
            _log.Append(line).Append('\n');
        }

        _pendingLogLines.Clear();

        if (_log.Length > LogCharacterLimit)
        {
            TrimLogToLineBoundary();
        }

        LogText = _log.ToString();
    }

    /// <summary>Drops whole lines from the front of <see cref="_log"/> until it is at or below
    /// <see cref="LogTrimTarget"/> characters, cutting only at a <c>'\n'</c> so no line is left
    /// half-truncated.</summary>
    private void TrimLogToLineBoundary()
    {
        var excess = _log.Length - LogTrimTarget;
        var text = _log.ToString();
        var cut = text.IndexOf('\n', Math.Min(excess, text.Length));
        _log.Remove(0, cut < 0 ? _log.Length : cut + 1);
    }

    private static void PlayFinishedSound()
    {
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Best-effort completion chime; a machine with no audio device must not break the
            // update flow. Static (no _logger capture needed) since there is nothing actionable to
            // log beyond "no sound played".
            Debug.WriteLine("Could not play the update-finished sound: " + ex.Message);
        }
    }

    private void OnPackagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (UpdatePackageViewModel row in e.OldItems)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (UpdatePackageViewModel row in e.NewItems)
            {
                row.PropertyChanged += OnRowPropertyChanged;
            }
        }

        RecomputeSelectedCount();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UpdatePackageViewModel.IsSelected) or nameof(UpdatePackageViewModel.IsIgnored))
        {
            RecomputeSelectedCount();
        }
    }

    private void RecomputeSelectedCount() => SelectedCount = Packages.Count(p => p.IsSelected && !p.IsIgnored);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    private partial void LogAppendedLine(string line);

    /// <summary>Idempotent: page view models are disposed twice on host shutdown (see
    /// <see cref="Shell.PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/>), and a second
    /// <see cref="CancellationTokenSource.Cancel()"/> on a disposed source would throw.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PorchlightUpdate.PropertyChanged -= OnPorchlightUpdateChanged;
        PorchlightUpdate.Dispose();
        _logFlushTimer.Stop();
        Packages.CollectionChanged -= OnPackagesCollectionChanged;
        foreach (var row in Packages)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        _refreshCts.Cancel();
        _refreshCts.Dispose();
    }
}
