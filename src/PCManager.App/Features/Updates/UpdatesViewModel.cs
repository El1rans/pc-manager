using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.App.Shell;
using PCManager.Core.Settings;
using PCManager.Core.Winget;

namespace PCManager.App.Features.Updates;

/// <summary>View model for the Updates page: lists <c>winget upgrade</c> results, lets the user
/// pick which to install, and runs them one at a time. See <c>docs/specs/02-updates.md</c>.</summary>
public sealed partial class UpdatesViewModel : PageViewModelBase, IDisposable
{
    /// <summary>Log panel cap - see <c>docs/specs/02-updates.md</c>.</summary>
    private const int LogCharacterLimit = 200_000;

    private readonly IWingetClient _wingetClient;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly Lock _logLock = new();
    private readonly StringBuilder _log = new();
    private readonly Lock _initialCheckLock = new();

    private CancellationTokenSource _refreshCts = new();
    private Task? _initialCheckTask;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopAfterCurrentCommand))]
    private bool _isUpdating;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopAfterCurrentCommand))]
    private bool _isStopRequested;

    [ObservableProperty]
    private string? _progressText;

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
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _isLogExpanded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedButtonText))]
    private int _selectedCount;

    /// <summary>"Update selected (N)" - a plain computed property (rather than an inline XAML
    /// <c>StringFormat</c>) so the exact button text is simple to assert on directly.</summary>
    public string UpdateSelectedButtonText =>
        $"Update selected ({SelectedCount.ToString(CultureInfo.InvariantCulture)})";

    public UpdatesViewModel(IWingetClient wingetClient, ISettingsStore settingsStore, ILogger<UpdatesViewModel> logger)
    {
        _wingetClient = wingetClient;
        _settingsStore = settingsStore;
        _logger = logger;

        // Bypasses the On*Changed hooks below (direct field access, not the generated property
        // setter) - loading saved settings must not immediately persist them again or kick off an
        // extra refresh.
        var updates = _settingsStore.Current.Updates;
        _silent = updates.Silent;
        _includeUnknown = updates.IncludeUnknown;

        Packages = [];
        Packages.CollectionChanged += OnPackagesCollectionChanged;
        PackagesView = CollectionViewSource.GetDefaultView(Packages);
        PackagesView.Filter = FilterPackage;
    }

    public override string Title => "Updates";

    public override string Glyph => "";

    public override int Order => 1;

    public ObservableCollection<UpdatePackageViewModel> Packages { get; }

    public ICollectionView PackagesView { get; }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => EnsureInitialCheckStartedAsync();

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
    /// successfully) instead of resetting, and the "Checking for updates..." progress message is
    /// not shown.</param>
    public async Task RefreshAsync(bool quiet)
    {
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _refreshCts = new CancellationTokenSource();
        var cancellationToken = _refreshCts.Token;

        IsBusy = true;
        if (!quiet)
        {
            ProgressText = "Checking for updates...";
        }

        try
        {
            var progress = new Progress<string>(text => ProgressText = text);
            var packages = await _wingetClient.GetUpgradesAsync(IncludeUnknown, progress, cancellationToken)
                .ConfigureAwait(true);
            MergePackages(packages, quiet);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh (or the app is shutting down); expected, not an error.
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
            SummaryText = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check for updates.");
            AppendLog("ERROR: " + ex.Message);
            SummaryText = "Could not check for updates.";
        }
        finally
        {
            IsBusy = false;
            if (!quiet)
            {
                ProgressText = null;
            }
        }
    }

    private void MergePackages(IReadOnlyList<WingetPackage> packages, bool quiet)
    {
        var ignoredIds = new HashSet<string>(_settingsStore.Current.Updates.IgnoredIds, StringComparer.OrdinalIgnoreCase);
        var previous = Packages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        Packages.Clear();
        foreach (var package in packages)
        {
            var isIgnored = ignoredIds.Contains(package.Id);
            var row = new UpdatePackageViewModel(package) { IsIgnored = isIgnored };

            if (previous.TryGetValue(package.Id, out var oldRow))
            {
                // A row that just finished successfully never reappears here (it no longer has an
                // upgrade available) - this only matters for a row still mid-run when a concurrent
                // refresh landed, which should not stay ticked.
                row.IsSelected = oldRow.IsSelected && oldRow.State != UpdateRowState.Updated;
                if (quiet)
                {
                    row.State = oldRow.State;
                    row.StatusText = oldRow.StatusText;
                }
            }
            else
            {
                row.IsSelected = !isIgnored && !package.RequiresExplicit;
            }

            Packages.Add(row);
        }

        UpdateSummary();
        UpdateBadge();
    }

    private void UpdateSummary()
    {
        var visible = Packages.Count(p => !p.IsIgnored);
        var ignored = Packages.Count(p => p.IsIgnored);

        var text = visible == 1 ? "1 update available" : $"{visible} updates available";
        if (ignored > 0)
        {
            text += ignored == 1 ? " (1 ignored)" : $" ({ignored} ignored)";
        }

        text += " - last checked " + DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        SummaryText = text;
    }

    private void UpdateBadge()
    {
        var count = Packages.Count(p => !p.IsIgnored);
        Badge = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : null;
    }

    private bool FilterPackage(object item)
    {
        if (item is not UpdatePackageViewModel row)
        {
            return false;
        }

        if (row.IsIgnored && !ShowIgnored)
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
        _ = RefreshAsync(quiet: false);
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task Refresh() => RefreshAsync(quiet: false);

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

        var updated = 0;
        var failed = 0;
        var skipped = 0;
        var completed = 0;

        var logProgress = new Progress<string>(AppendLog);
        var stepProgress = new Progress<string>(text => ProgressText = text);

        foreach (var row in selected)
        {
            if (IsStopRequested)
            {
                row.State = UpdateRowState.Skipped;
                row.StatusText = "Cancelled";
                skipped++;
                continue;
            }

            completed++;
            row.State = UpdateRowState.Updating;
            row.StatusText = "Updating...";
            ProgressText = $"[{completed}/{selected.Count}] Updating {row.Id}";
            AppendLog($"> winget upgrade --id {row.Id} --exact");

            try
            {
                // Always CancellationToken.None: once winget has actually launched, killing it
                // mid-install can leave the package half-installed. "Stop after current" is the
                // IsStopRequested flag checked above, only honoured between packages - see
                // IWingetClient.UpgradeAsync.
                var result = await _wingetClient
                    .UpgradeAsync(row.Id, Silent, logProgress, stepProgress, CancellationToken.None)
                    .ConfigureAwait(true);

                var (outcome, message) = WingetExitCodes.Describe(result.ExitCode);
                if (outcome == PackageOutcome.Success && WingetExitCodes.MentionsRestart(result.Lines))
                {
                    message = "Updated - restart needed";
                }

                row.StatusText = message;
                row.State = outcome switch
                {
                    PackageOutcome.Success => UpdateRowState.Updated,
                    PackageOutcome.Skipped => UpdateRowState.Skipped,
                    _ => UpdateRowState.Failed,
                };

                switch (outcome)
                {
                    case PackageOutcome.Success:
                        updated++;
                        break;
                    case PackageOutcome.Skipped:
                        skipped++;
                        break;
                    default:
                        failed++;
                        break;
                }

                AppendLog($"<< {row.Id}: exit 0x{unchecked((uint)result.ExitCode):X8}");
                AppendLog(string.Empty);
            }
            catch (WingetNotFoundException ex)
            {
                row.State = UpdateRowState.Failed;
                row.StatusText = "Failed";
                failed++;
                AppendLog("ERROR: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure updating {PackageId}.", row.Id);
                row.State = UpdateRowState.Failed;
                row.StatusText = "Failed";
                failed++;
                AppendLog("ERROR: " + ex.Message);
            }
        }

        var summary = $"Finished: {updated} updated, {failed} failed, {skipped} skipped";
        ProgressText = summary;
        AppendLog(summary);
        PlayFinishedSound();

        IsUpdating = false;
        IsBusy = false;

        await RefreshAsync(quiet: true).ConfigureAwait(true);
    }

    private bool CanUpdateSelected() => !IsBusy && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanStopAfterCurrent))]
    private void StopAfterCurrent()
    {
        IsStopRequested = true;
        ProgressText = "Stopping after the current app finishes...";
    }

    private bool CanStopAfterCurrent() => IsUpdating && !IsStopRequested;

    /// <summary>Adds the given ids to the ignore list (persisted), unselects and marks those rows
    /// ignored, and refreshes the summary/badge/filter. Called from the view's context menu with
    /// the DataGrid's currently selected rows.</summary>
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

    /// <summary>Runs <c>winget show</c> for one package into the Log panel (expanding it first) -
    /// the "Show package info" context menu item.</summary>
    public async Task ShowPackageInfoAsync(UpdatePackageViewModel row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        IsLogExpanded = true;
        AppendLog($"> winget show --id {row.Id} --exact");

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
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCManager", "logs");
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Could not open the log folder at {Path}.", path);
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        lock (_logLock)
        {
            _log.Clear();
        }

        LogText = string.Empty;
    }

    /// <summary>Appends one line to the Log panel (capped at <see cref="LogCharacterLimit"/>
    /// characters) and mirrors it to the app log - see <c>docs/specs/02-updates.md</c>: "Everything
    /// written to the Log panel also goes to the app log."</summary>
    private void AppendLog(string line)
    {
        LogAppendedLine(line);

        lock (_logLock)
        {
            _log.Append(line).Append('\n');
            if (_log.Length > LogCharacterLimit)
            {
                _log.Remove(0, _log.Length - LogCharacterLimit);
            }

            LogText = _log.ToString();
        }
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

    public void Dispose()
    {
        Packages.CollectionChanged -= OnPackagesCollectionChanged;
        foreach (var row in Packages)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        _refreshCts.Cancel();
        _refreshCts.Dispose();
    }
}
