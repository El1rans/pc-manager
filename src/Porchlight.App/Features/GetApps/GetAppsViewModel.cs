using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Shell;
using Porchlight.Core.Processes;
using Porchlight.Core.Settings;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.GetApps;

/// <summary>
/// View model for the "Get apps" page: search winget's community source, or pick from a short
/// curated list, and install one app at a time. See <c>docs/specs/27-get-apps.md</c>.
/// </summary>
/// <remarks>
/// Only ids shown on this page (the last search or the popular list) can be installed. An install
/// is never cancelled once started - leaving the page does not stop it and <see cref="IBusyGuard"/>
/// warns before the app closes.
/// </remarks>
public sealed partial class GetAppsViewModel : PageViewModelBase, IBusyGuard, IDisposable
{
    /// <summary>Searching needs at least this many characters.</summary>
    public const int MinimumQueryLength = 2;

    /// <summary>Most results shown (matches the <c>--count</c> passed to winget).</summary>
    public const int MaximumResults = 50;

    public const string SearchingText = "Searching...";
    public const string QueryTooShortText = "Type at least 2 characters to search.";
    public const string SearchFailedText = "Could not search for apps. Check your internet connection and try again.";
    public const string InstallFailedText = "Something went wrong while installing. Details are in the log.";

    private readonly IWingetClient _wingetClient;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<GetAppsViewModel> _logger;

    private HashSet<string> _installedIds = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _searchCts = new();
    private bool _installRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPopular))]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    /// <summary>True once a search has produced a list (even an empty one); false shows the popular apps.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPopular))]
    private bool _hasSearched;

    [ObservableProperty]
    private string? _statusText;

    /// <summary>Set when winget is missing or the search failed; shown with a warning icon.</summary>
    [ObservableProperty]
    private string? _errorText;

    public GetAppsViewModel(IWingetClient wingetClient, ISettingsStore settingsStore, ILogger<GetAppsViewModel> logger)
    {
        _wingetClient = wingetClient;
        _settingsStore = settingsStore;
        _logger = logger;

        Results = [];
        PopularApps =
        [
            .. Porchlight.Core.Winget.PopularApps.All.Select(a => CreateRow(a.Name, a.Id, a.Version)),
        ];
    }

    public override string Title => "Get apps";

    public override string Glyph => "";

    public override int Order => 1;

    public override PageCategory Category => PageCategory.Apps;

    public ObservableCollection<AppResultViewModel> Results { get; }

    public ObservableCollection<AppResultViewModel> PopularApps { get; }

    public bool ShowPopular => !HasSearched;

    /// <inheritdoc/>
    public bool IsBusyWithWork => _installRunning;

    /// <inheritdoc/>
    public string BusyMessage =>
        "An app is still installing. If you close Porchlight now, the installer keeps running " +
        "but you won't see the result. Close anyway?";

    /// <summary>Completes when the once-per-visit "which of these are already installed" check
    /// finishes (a test seam; the page never waits for it).</summary>
    public Task InstalledIdsTask { get; private set; } = Task.CompletedTask;

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Background by design: until it finishes the Installed state is simply not shown.
        InstalledIdsTask = LoadInstalledIdsAsync();
        return Task.CompletedTask;
    }

    partial void OnQueryChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        // Emptying the box goes back to the popular list instead of leaving stale results around.
        CancelSearch();
        Results.Clear();
        HasSearched = false;
        IsSearching = false;
        StatusText = null;
        ErrorText = null;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            return;
        }

        if (query.Length < MinimumQueryLength)
        {
            StatusText = QueryTooShortText;
            return;
        }

        CancelSearch();
        var cts = _searchCts;

        IsSearching = true;
        ErrorText = null;
        StatusText = SearchingText;

        try
        {
            var found = await _wingetClient.SearchAsync(query, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Results.Clear();
            foreach (var item in found.Take(MaximumResults))
            {
                Results.Add(CreateRow(item.Name, item.Id, item.Version));
            }

            HasSearched = true;
            StatusText = Results.Count == 0
                ? $"No apps found for \"{query}\". Check the spelling or try a shorter name."
                : Results.Count == 1
                    ? "1 app found"
                    : string.Create(CultureInfo.CurrentCulture, $"{Results.Count} apps found");
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search (or the box was cleared); that one owns the state now.
        }
        catch (WingetNotFoundException ex)
        {
            StatusText = null;
            ErrorText = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Searching winget for \"{Query}\" failed.", query);
            StatusText = null;
            ErrorText = SearchFailedText;
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts))
            {
                IsSearching = false;
            }
        }
    }

    /// <summary>Installs <paramref name="row"/> if (and only if) it is one of the rows currently
    /// shown and no other install is running.</summary>
    public async Task InstallAsync(AppResultViewModel row)
    {
        if (_installRunning || row.IsInstalled || !IsOfferedRow(row))
        {
            return;
        }

        SetInstallRunning(true);
        row.IsInstalling = true;
        row.ResultText = null;
        row.ResultIsError = false;
        row.ProgressText = null;
        ErrorText = null;

        var progress = new Progress<string>(text => row.ProgressText = text);
        var silent = _settingsStore.Current.Updates.Silent;
        var succeeded = false;

        try
        {
            // CancellationToken.None on purpose: killing winget mid-install can leave a half-installed app.
            var result = await _wingetClient
                .InstallAsync(row.Id, silent, log: null, progress, CancellationToken.None)
                .ConfigureAwait(true);
            succeeded = ApplyResult(row, result);
        }
        catch (WingetNotFoundException ex)
        {
            row.ResultText = ex.Message;
            row.ResultIsError = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure installing {PackageId}.", row.Id);
            row.ResultText = InstallFailedText;
            row.ResultIsError = true;
        }
        finally
        {
            row.IsInstalling = false;
            row.ProgressText = null;
            SetInstallRunning(false);
        }

        if (succeeded)
        {
            MarkInstalled(row.Id);
        }
    }

    private bool ApplyResult(AppResultViewModel row, WingetResult result)
    {
        var outcome = WingetExitCodes.DescribeOutcome(result.ExitCode, result.Lines);
        LogInstallFinished(row.Id, outcome.Title, outcome.ExitCodeHex);

        if (outcome.Kind == WingetOutcomeKind.Updated
            || result.ExitCode is WingetExitCodes.PackageAlreadyInstalled or WingetExitCodes.InstallAlreadyInstalled)
        {
            row.ResultText = AppResultViewModel.InstalledText;
            row.ResultIsError = false;
            return true;
        }

        if (outcome.Kind == WingetOutcomeKind.UpdatedRestartNeeded)
        {
            row.ResultText = "Installed. Restart your PC to finish.";
            row.ResultIsError = false;
            return true;
        }

        // Spec 09's outcome kinds, worded for a first install rather than an update; the winget
        // code stays in the log.
        row.ResultText = DescribeInstallFailure(outcome.Kind);
        row.ResultIsError = true;
        return false;
    }

    public static string DescribeInstallFailure(WingetOutcomeKind kind) => kind switch
    {
        WingetOutcomeKind.NoApplicableUpdate => "This app isn't available for this PC.",
        WingetOutcomeKind.AppRunning => "The installer needs a file that's in use. Close other apps and try again.",
        WingetOutcomeKind.Cancelled => "The install was cancelled. Press Install to try again.",
        WingetOutcomeKind.NeedsAdmin => "The install needs administrator approval. Try again and choose Yes when Windows asks.",
        WingetOutcomeKind.Blocked => "Your organization's settings block installing this app.",
        WingetOutcomeKind.NetworkProblem => "Couldn't download the app. Check your internet connection and try again.",
        _ => "The install didn't work. Restart your PC and try again.",
    };

    private void MarkInstalled(string id)
    {
        _installedIds.Add(id);
        ApplyInstalledState();
    }

    private async Task LoadInstalledIdsAsync()
    {
        try
        {
            var ids = await _wingetClient.ListInstalledIdsAsync(CancellationToken.None).ConfigureAwait(true);

            // Keep anything installed during this visit that the (earlier-started) listing missed.
            var merged = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            merged.UnionWith(_installedIds);
            _installedIds = merged;
            ApplyInstalledState();
        }
        catch (Exception ex)
        {
            // Only the "Installed" labels are lost; searching and installing still work.
            _logger.LogDebug(ex, "Could not list installed apps; Installed state will not be shown.");
        }
    }

    private void ApplyInstalledState()
    {
        foreach (var row in Results.Concat(PopularApps))
        {
            if (_installedIds.Contains(row.Id))
            {
                row.IsInstalled = true;
            }
        }
    }

    private AppResultViewModel CreateRow(string name, string id, string version) =>
        new(name, id, version, InstallAsync, () => _installRunning)
        {
            IsInstalled = _installedIds.Contains(id),
        };

    private bool IsOfferedRow(AppResultViewModel row) =>
        (Results.Contains(row) || PopularApps.Contains(row))
        && !string.IsNullOrWhiteSpace(row.Id)
        && !row.Id.StartsWith('-');

    private void SetInstallRunning(bool value)
    {
        _installRunning = value;
        OnPropertyChanged(nameof(IsBusyWithWork));
        foreach (var row in Results.Concat(PopularApps))
        {
            row.RefreshCanInstall();
        }
    }

    public void Dispose() => _searchCts.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "Install of {PackageId} finished: {Title} (winget code {ExitCode}).")]
    private partial void LogInstallFinished(string packageId, string title, string exitCode);

    private void CancelSearch()
    {
        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();
    }
}
