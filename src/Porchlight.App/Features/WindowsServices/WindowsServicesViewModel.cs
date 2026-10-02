using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.WindowsServices;

namespace Porchlight.App.Features.WindowsServices;

/// <summary>"Services" page: explains background services in plain words and lets the user stop or
/// turn off the ones that came with other apps. See docs/specs/29-windows-services.md.</summary>
public sealed partial class WindowsServicesViewModel : PageViewModelBase
{
    private const string LoadFailedMessage = "Couldn't read the services list. Try Refresh.";
    private const string ChangeFailedMessage = "Couldn't change this one. Try again, or restart it from Windows' Services app.";
    private const string NeedsAdminMessage = "Changing a service needs administrator rights. Use Restart as administrator above.";
    private const string RefusedMessage = "Porchlight doesn't change this service.";
    private const string NotFoundMessage = "This service is gone. Refresh the list.";
    private const string StopWarning = "The app that installed this may stop working properly until you turn it back on.";
    private const string RestartWarning = "The app that installed this may stop working for a moment while it restarts.";

    private readonly IWindowsServicesService _service;
    private readonly IShellService _shell;
    private readonly IConfirmationDialog _confirm;
    private readonly ILogger<WindowsServicesViewModel> _logger;
    private List<ServiceRowViewModel> _allRows = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(ShowEmptyState))]
    private bool _hasLoaded;

    [ObservableProperty]
    private bool _showWindowsServices;

    [ObservableProperty]
    private string _filter = string.Empty;

    public WindowsServicesViewModel(
        IWindowsServicesService service,
        IShellService shell,
        IConfirmationDialog confirm,
        ILogger<WindowsServicesViewModel> logger)
    {
        _service = service;
        _shell = shell;
        _confirm = confirm;
        _logger = logger;
    }

    public override string Title => "Services";

    // Segoe Fluent Icons "Settings" (gear).
    public override string Glyph => "";

    public override int Order => 3;

    public override PageCategory Category => PageCategory.Apps;

    /// <summary>The rows currently shown (after the Windows-services toggle and the search filter).</summary>
    public ObservableCollection<ServiceRowViewModel> Services { get; } = [];

    public bool IsElevated => _shell.IsElevated;

    public bool ShowAdminBanner => !IsElevated && Services.Any(s => s.IsChangeable);

    public bool ShowEmptyState => HasLoaded && !IsLoading && Services.Count == 0 && ErrorMessage is null;

    public string Summary
    {
        get
        {
            if (!HasLoaded)
            {
                return string.Empty;
            }

            var others = _allRows.Where(r => !r.IsMicrosoft).ToList();
            var running = others.Count(r => r.State == ServiceRunState.Running);
            var noun = others.Count == 1 ? "service" : "services";
            return $"{others.Count} {noun} from other apps - {running} running";
        }
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    partial void OnShowWindowsServicesChanged(bool value) => ApplyFilter();

    partial void OnFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private Task StartAsync(ServiceRowViewModel? row) =>
        row is null
            ? Task.CompletedTask
            : RunAsync(row, "started", ct => _service.StartAsync(row.Name, ct));

    [RelayCommand]
    private async Task StopAsync(ServiceRowViewModel? row)
    {
        if (row is null || !_confirm.Confirm($"Stop {row.DisplayName}?", StopWarning))
        {
            return;
        }

        await RunAsync(row, "stopped", ct => _service.StopAsync(row.Name, ct));
    }

    [RelayCommand]
    private async Task RestartAsync(ServiceRowViewModel? row)
    {
        if (row is null || !_confirm.Confirm($"Restart {row.DisplayName}?", RestartWarning))
        {
            return;
        }

        await RunAsync(row, "restarted", ct => _service.RestartAsync(row.Name, ct));
    }

    private async Task OnStartTypeSelectedAsync(ServiceRowViewModel row, StartTypeOption previous, StartTypeOption selected)
    {
        // Turning a service off is the risky one: ask first, and put the combo back if declined.
        if (selected.Type == ServiceStartType.Disabled && !_confirm.Confirm($"Turn off {row.DisplayName}?", StopWarning))
        {
            row.RevertStartOption(previous);
            return;
        }

        var changed = await RunAsync(row, $"set to \"{selected.Label}\"", ct => _service.SetStartTypeAsync(row.Name, selected.Type, ct));
        if (!changed)
        {
            row.RevertStartOption(previous);
        }
    }

    private async Task<bool> RunAsync(ServiceRowViewModel row, string doneText, Func<CancellationToken, Task<ServiceChangeOutcome>> change)
    {
        Message = null;
        ErrorMessage = null;
        row.IsBusy = true;

        ServiceChangeOutcome outcome;
        try
        {
            outcome = await change(CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Changing a service failed unexpectedly.");
            ErrorMessage = ChangeFailedMessage;
            return false;
        }
        finally
        {
            row.IsBusy = false;
        }

        switch (outcome.Result)
        {
            case ServiceChangeResult.Changed:
                Message = $"{row.DisplayName} {doneText}.";
                await RefreshQuietlyAsync();
                return true;
            case ServiceChangeResult.NeedsAdmin:
                ErrorMessage = NeedsAdminMessage;
                return false;
            case ServiceChangeResult.Refused:
                ErrorMessage = RefusedMessage;
                return false;
            case ServiceChangeResult.NotFound:
                ErrorMessage = NotFoundMessage;
                return false;
            case ServiceChangeResult.TimedOut:
                ErrorMessage = $"{row.DisplayName} didn't respond in time. Check its state with Refresh.";
                return false;
            case ServiceChangeResult.HasDependents:
                ErrorMessage = $"Can't stop {row.DisplayName} while {string.Join(", ", outcome.Dependents)} still need it. Stop those first.";
                return false;
            default:
                ErrorMessage = ChangeFailedMessage;
                return false;
        }
    }

    /// <summary>Re-reads the list after a change without the page's loading state or clearing messages.</summary>
    private async Task RefreshQuietlyAsync()
    {
        try
        {
            Rebuild(await _service.ListAsync(CancellationToken.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Re-reading the services list after a change failed.");
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
            // Navigated away or shutting down; nothing to show.
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Reading the services list failed.");
            ErrorMessage = LoadFailedMessage;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void Rebuild(IReadOnlyList<WindowsServiceEntry> entries)
    {
        var elevated = IsElevated;
        _allRows = [.. entries.Select(e => new ServiceRowViewModel(e, elevated, OnStartTypeSelectedAsync))];
        HasLoaded = true;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = Filter.Trim();
        var visible = _allRows.Where(r =>
            (ShowWindowsServices || !r.IsMicrosoft)
            && (text.Length == 0
                || r.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Publisher.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Description.Contains(text, StringComparison.OrdinalIgnoreCase)));

        Services.Clear();
        foreach (var row in visible)
        {
            Services.Add(row);
        }

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ShowAdminBanner));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
