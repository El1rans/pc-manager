using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.RemoveApps;

namespace Porchlight.App.Features.RemoveApps;

/// <summary>"Remove apps" page: a plain list of installed programs with a careful Remove button.
/// See docs/specs/35-remove-apps.md.</summary>
public sealed partial class RemoveAppsViewModel : PageViewModelBase, IBusyGuard
{
    public const string LoadFailedMessage = "Couldn't read the list of apps. Try Refresh.";
    public const string RefusedMessage = "Porchlight doesn't remove this one.";
    public const string BlockedWhileElevatedMessage =
        "Porchlight is running as administrator, so it can't remove this app. Restart Porchlight normally and try again.";
    public const string InvalidCommandMessage =
        "This app's uninstaller couldn't be started. You can remove it from Windows Settings, under Installed apps.";
    public const string SystemPartWarning = "Windows or other apps may need this to work.";
    public const string ConfirmSuffix = "This can't be undone from Porchlight.";

    private readonly IRemoveAppsService _service;
    private readonly IConfirmationDialog _confirm;
    private readonly TimeProvider _time;
    private readonly ILogger<RemoveAppsViewModel> _logger;
    private List<AppRowViewModel> _allRows = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private RemoveAppsSort _selectedSort = RemoveAppsSort.Name;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusyWithWork))]
    private bool _isRemoving;

    public RemoveAppsViewModel(
        IRemoveAppsService service,
        IConfirmationDialog confirm,
        TimeProvider time,
        ILogger<RemoveAppsViewModel> logger)
    {
        _service = service;
        _confirm = confirm;
        _time = time;
        _logger = logger;
    }

    public override string Title => "Remove apps";

    // Segoe Fluent Icons "Delete".
    public override string Glyph => "";

    public override int Order => 4;

    public override PageCategory Category => PageCategory.Apps;

    public ObservableCollection<AppRowViewModel> Apps { get; } = [];

    public ObservableCollection<AppRowViewModel> SystemParts { get; } = [];

    public IReadOnlyList<RemoveAppsSortOption> SortOptions { get; } =
    [
        new(RemoveAppsSort.Name, "Name"),
        new(RemoveAppsSort.Size, "Size"),
        new(RemoveAppsSort.Date, "Date installed"),
    ];

    public bool IsBusyWithWork => IsRemoving;

    public string BusyMessage => "Porchlight is removing an app. If you close Porchlight now, the removal keeps going without it.";

    public bool ShowEmptyState => HasLoaded && !IsLoading && Apps.Count == 0 && SystemParts.Count == 0 && ErrorMessage is null;

    public bool HasSystemParts => SystemParts.Count > 0;

    public string SystemPartsHeader => $"System parts - usually keep ({SystemParts.Count})";

    public string Summary => HasLoaded ? $"{_allRows.Count(r => !IsSystemPart(r))} apps installed" : string.Empty;

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    partial void OnFilterChanged(string value) => ApplyView();

    partial void OnSelectedSortChanged(RemoveAppsSort value) => ApplyView();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private async Task RemoveAsync(AppRowViewModel? row)
    {
        if (row is null || IsRemoving || !row.CanRemove)
        {
            return;
        }

        var text = row.App.Kind == RemovableAppKind.SystemPart
            ? $"{SystemPartWarning} {ConfirmSuffix}"
            : ConfirmSuffix;
        if (!_confirm.Confirm($"Remove {row.Name}?", text))
        {
            return;
        }

        Message = null;
        ErrorMessage = null;
        IsRemoving = true;
        row.IsBusy = true;
        RemoveAppOutcome outcome;
        try
        {
            outcome = await _service.RemoveAsync(row.App, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Removing {App} failed unexpectedly.", row.Name);
            ErrorMessage = FailedMessage(row.Name);
            return;
        }
        finally
        {
            row.IsBusy = false;
            IsRemoving = false;
        }

        await ShowOutcomeAsync(row, outcome);
    }

    private async Task ShowOutcomeAsync(AppRowViewModel row, RemoveAppOutcome outcome)
    {
        switch (outcome.Result)
        {
            case RemoveAppResult.Removed:
                Message = $"{row.Name} was removed.";
                await ReloadQuietlyAsync();
                break;
            case RemoveAppResult.UninstallerOpened:
                Message = $"The uninstaller for {row.Name} opened. Finish it there, then press Refresh.";
                break;
            case RemoveAppResult.Refused:
                ErrorMessage = RefusedMessage;
                break;
            case RemoveAppResult.BlockedWhileElevated:
                ErrorMessage = BlockedWhileElevatedMessage;
                break;
            case RemoveAppResult.InvalidCommand:
                ErrorMessage = InvalidCommandMessage;
                break;
            case RemoveAppResult.WingetProblem when outcome.WingetOutcome is { } winget:
                ErrorMessage = $"{winget.Title}. {winget.Explanation}";
                break;
            default:
                ErrorMessage = FailedMessage(row.Name);
                break;
        }
    }

    private static string FailedMessage(string name) =>
        $"Couldn't remove {name}. If Windows asked for permission, choose Yes and try again.";

    private async Task ReloadQuietlyAsync()
    {
        try
        {
            Rebuild(await _service.ListAsync(CancellationToken.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Re-reading the apps list after a removal failed.");
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Rebuild(await _service.ListAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Reading the apps list failed.");
            ErrorMessage = LoadFailedMessage;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void Rebuild(IReadOnlyList<RemovableApp> apps)
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        _allRows = [.. apps.Select(a => new AppRowViewModel(a, today))];
        HasLoaded = true;
        ApplyView();
    }

    private static bool IsSystemPart(AppRowViewModel row) => row.App.Kind == RemovableAppKind.SystemPart;

    private void ApplyView()
    {
        var text = Filter.Trim();
        var matching = _allRows.Where(r =>
            text.Length == 0
            || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || r.Publisher.Contains(text, StringComparison.OrdinalIgnoreCase));

        var ordered = Sort(matching).ToList();
        Replace(Apps, ordered.Where(r => !IsSystemPart(r)));
        Replace(SystemParts, ordered.Where(IsSystemPart));

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSystemParts));
        OnPropertyChanged(nameof(SystemPartsHeader));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private IEnumerable<AppRowViewModel> Sort(IEnumerable<AppRowViewModel> rows) => SelectedSort switch
    {
        RemoveAppsSort.Size => rows
            .OrderByDescending(r => r.SizeBytes ?? -1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
        RemoveAppsSort.Date => rows
            .OrderByDescending(r => r.InstallDate?.DayNumber ?? -1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
        _ => rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
    };

    private static void Replace(ObservableCollection<AppRowViewModel> target, IEnumerable<AppRowViewModel> rows)
    {
        target.Clear();
        foreach (var row in rows)
        {
            target.Add(row);
        }
    }
}
