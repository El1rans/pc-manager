using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Printing;

namespace Porchlight.App.Features.Printers;

/// <summary>"Printers" page: "My printer won't print". Lists printers in plain words and offers the
/// usual fixes (clear stuck jobs, restart the print service, a guided fix). See
/// docs/specs/36-printer-fixes.md.</summary>
public sealed partial class PrintersViewModel : PageViewModelBase
{
    public const string PrinterSettingsUri = "ms-settings:printers";

    private const string LoadFailedMessage = "Couldn't read the printers. Try Refresh.";
    private const string ChangeFailedMessage = "That didn't work. Try again, or use Windows' printer settings.";
    private const string NeedsAdminMessage = "That needs administrator rights. Use Restart as administrator above.";
    private const string NotFoundMessage = "This printer is gone. Refresh the list.";
    private const string TimedOutMessage = "Windows didn't answer in time. Try again in a moment.";
    private const string OfflineNotSupportedMessage =
        "Windows won't let Porchlight change this. Printer settings are open: pick the printer, choose Open print queue, then turn off \"Use printer offline\".";

    private readonly IPrinterService _service;
    private readonly IPrinterActions _actions;
    private readonly PrinterFixFlow _fixFlow;
    private readonly IShellService _shell;
    private readonly IConfirmationDialog _confirm;
    private readonly IUrlLauncher _launcher;
    private readonly ILogger<PrintersViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(DefaultPrinterText))]
    private bool _hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestartService), nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _fixSummary;

    public PrintersViewModel(
        IPrinterService service,
        IPrinterActions actions,
        PrinterFixFlow fixFlow,
        IShellService shell,
        IConfirmationDialog confirm,
        IUrlLauncher launcher,
        ILogger<PrintersViewModel> logger)
    {
        _service = service;
        _actions = actions;
        _fixFlow = fixFlow;
        _shell = shell;
        _confirm = confirm;
        _launcher = launcher;
        _logger = logger;
    }

    public override string Title => "Printers";

    // Segoe Fluent Icons "Print".
    public override string Glyph => "";

    public override int Order => 3;

    public override PageCategory Category => PageCategory.Hardware;

    /// <summary>Real printers (default first).</summary>
    public ObservableCollection<PrinterRowViewModel> Printers { get; } = [];

    /// <summary>Virtual printers (PDF, XPS, OneNote, Fax): kept out of the way.</summary>
    public ObservableCollection<PrinterRowViewModel> OtherPrinters { get; } = [];

    /// <summary>Report lines of the last "Fix my printer" run.</summary>
    public ObservableCollection<FixStepRowViewModel> FixSteps { get; } = [];

    public bool IsElevated => _shell.IsElevated;

    public bool ShowAdminBanner => !IsElevated;

    public bool IsNotBusy => !IsBusy;

    public bool CanRestartService => IsElevated && !IsBusy;

    public bool HasOtherPrinters => OtherPrinters.Count > 0;

    public bool HasFixSteps => FixSteps.Count > 0;

    public bool ShowEmptyState => HasLoaded && !IsLoading && Printers.Count == 0 && ErrorMessage is null;

    public string DefaultPrinterText
    {
        get
        {
            if (!HasLoaded)
            {
                return string.Empty;
            }

            var name = DefaultPrinterName;
            return name is null ? "No default printer is set." : $"Default printer: {name}";
        }
    }

    private string? DefaultPrinterName =>
        Printers.Concat(OtherPrinters).FirstOrDefault(p => p.IsDefault)?.Name;

    private int WaitingJobs => Printers.Concat(OtherPrinters).Sum(p => p.Entry.JobCount);

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private void OpenSettings() => _launcher.Open(PrinterSettingsUri);

    [RelayCommand]
    private Task MakeDefaultAsync(PrinterRowViewModel? row) =>
        row is null || IsBusy
            ? Task.CompletedTask
            : RunAsync(row, ct => _actions.SetDefaultAsync(row.Name, ct), $"{row.Name} is now your default printer.");

    [RelayCommand]
    private async Task ClearJobsAsync(PrinterRowViewModel? row)
    {
        if (row is null || IsBusy || !_confirm.Confirm(
                $"Clear print jobs for {row.Name}?",
                $"This cancels {row.Entry.JobCount} waiting document(s). They won't print unless you send them again."))
        {
            return;
        }

        await RunAsync(row, ct => _actions.ClearJobsAsync(row.Name, ct), $"Cleared the waiting jobs for {row.Name}.");
    }

    [RelayCommand]
    private async Task UseOnlineAsync(PrinterRowViewModel? row)
    {
        if (row is null || IsBusy)
        {
            return;
        }

        await RunAsync(row, ct => _actions.UseOnlineAsync(row.Name, ct), $"{row.Name} is set to be used online.", openSettingsIfUnsupported: true);
    }

    [RelayCommand]
    private Task PrintTestPageAsync(PrinterRowViewModel? row) =>
        row is null || IsBusy
            ? Task.CompletedTask
            : RunAsync(row, ct => _actions.PrintTestPageAsync(row.Name, ct), $"Sent a test page to {row.Name}.");

    [RelayCommand]
    private async Task RestartServiceAsync()
    {
        if (IsBusy || !CanRestartService)
        {
            return;
        }

        var clearFiles = WaitingJobs > 0;
        var detail = clearFiles
            ? "Documents waiting to print will be cancelled. The print service stops for a moment, then starts again."
            : "The print service stops for a moment, then starts again.";
        if (!_confirm.Confirm("Restart the print service?", detail))
        {
            return;
        }

        await RunAsync(null, ct => _actions.RestartSpoolerAsync(clearFiles, ct), "The print service was restarted.");
    }

    [RelayCommand]
    private async Task FixMyPrinterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var target = DefaultPrinterName;
        var detail = target is null
            ? "Porchlight will check the print service and restart it. Waiting print jobs may be cancelled."
            : $"Porchlight will check the print service, cancel everything waiting on {target}, and restart the print service.";
        if (!_confirm.Confirm("Fix my printer?", detail))
        {
            return;
        }

        IsBusy = true;
        Message = null;
        ErrorMessage = null;
        FixSummary = null;
        FixSteps.Clear();
        OnPropertyChanged(nameof(HasFixSteps));
        try
        {
            var report = await _fixFlow.RunAsync(
                target,
                new InlineProgress<PrinterFixStep>(step =>
                {
                    FixSteps.Add(new FixStepRowViewModel(step));
                    OnPropertyChanged(nameof(HasFixSteps));
                }),
                CancellationToken.None);
            FixSummary = report.Summary;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The guided printer fix failed unexpectedly.");
            ErrorMessage = ChangeFailedMessage;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshQuietlyAsync();
    }

    private async Task RunAsync(
        PrinterRowViewModel? row,
        Func<CancellationToken, Task<PrinterActionOutcome>> action,
        string doneText,
        bool openSettingsIfUnsupported = false)
    {
        Message = null;
        ErrorMessage = null;
        IsBusy = true;
        if (row is not null)
        {
            row.IsBusy = true;
        }

        PrinterActionOutcome outcome;
        try
        {
            outcome = await action(CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "A printer action failed unexpectedly.");
            ErrorMessage = ChangeFailedMessage;
            return;
        }
        finally
        {
            IsBusy = false;
            if (row is not null)
            {
                row.IsBusy = false;
            }
        }

        switch (outcome.Result)
        {
            case PrinterActionResult.Done:
                Message = doneText;
                await RefreshQuietlyAsync();
                break;
            case PrinterActionResult.NeedsAdmin:
                ErrorMessage = NeedsAdminMessage;
                break;
            case PrinterActionResult.NotFound:
                ErrorMessage = NotFoundMessage;
                break;
            case PrinterActionResult.TimedOut:
                ErrorMessage = TimedOutMessage;
                break;
            case PrinterActionResult.NotSupported when openSettingsIfUnsupported:
                _launcher.Open(PrinterSettingsUri);
                Message = OfflineNotSupportedMessage;
                break;
            default:
                ErrorMessage = ChangeFailedMessage;
                break;
        }
    }

    private async Task RefreshQuietlyAsync()
    {
        try
        {
            Rebuild(await _service.ListAsync(CancellationToken.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Re-reading the printers after a change failed.");
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
            _logger.LogWarning(ex, "Reading the printers failed.");
            ErrorMessage = LoadFailedMessage;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void Rebuild(IReadOnlyList<PrinterEntry> entries)
    {
        Printers.Clear();
        OtherPrinters.Clear();
        foreach (var entry in entries)
        {
            (entry.IsVirtual ? OtherPrinters : Printers).Add(new PrinterRowViewModel(entry));
        }

        HasLoaded = true;
        OnPropertyChanged(nameof(DefaultPrinterText));
        OnPropertyChanged(nameof(HasOtherPrinters));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowAdminBanner));
    }

    /// <summary>Reports on the calling context immediately (unlike <see cref="Progress{T}"/>, which
    /// posts), so steps appear in order and tests need no message pump.</summary>
    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
    }
}
