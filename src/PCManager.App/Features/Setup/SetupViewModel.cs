using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.Core.Components;
using PCManager.Core.Settings;

namespace PCManager.App.Features.Setup;

/// <summary>
/// View model for the first-run ("Choose what to set up") dialog, also reachable later from the
/// sidebar footer's "Set up optional features" button.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject, IDisposable
{
    private readonly IComponentService _componentService;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<SetupViewModel> _logger;
    private CancellationTokenSource _cts = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
    private bool _isBusy;

    public SetupViewModel(IComponentService componentService, ISettingsStore settingsStore, ILogger<SetupViewModel> logger)
    {
        _componentService = componentService;
        _settingsStore = settingsStore;
        _logger = logger;
        Items = new ObservableCollection<SetupItemViewModel>(
            ComponentCatalog.All.Select(d => new SetupItemViewModel(d)));
    }

    public ObservableCollection<SetupItemViewModel> Items { get; }

    /// <summary>Raised when the dialog should close (setup finished, or the user chose Skip).</summary>
    public event EventHandler? CloseRequested;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        foreach (var item in Items)
        {
            item.Status = await _componentService.GetStatusAsync(item.Definition.Id, cancellationToken)
                .ConfigureAwait(true);
            item.IsSelected = item.Status.State == ComponentState.NotInstalled;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSetUp))]
    private async Task SetUpAsync()
    {
        _cts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            foreach (var item in Items.Where(i => i.IsSelected && i.Status.State == ComponentState.NotInstalled))
            {
                item.ProgressMessage = "Installing...";
                var log = new Progress<string>(line => item.ProgressMessage = line);
                var progress = new Progress<string>(text => item.ProgressMessage = text);

                item.Status = await _componentService
                    .InstallAsync(item.Definition.Id, log, progress, _cts.Token)
                    .ConfigureAwait(true);

                item.ProgressMessage = item.Status.State == ComponentState.Error ? item.Status.Message : null;
            }
        }
        catch (OperationCanceledException)
        {
            // Dialog closed mid-install; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure during first-run setup.");
        }
        finally
        {
            IsBusy = false;
        }

        MarkFirstRunCompleted();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSetUp() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSkip))]
    private void Skip()
    {
        MarkFirstRunCompleted();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSkip() => !IsBusy;

    private void MarkFirstRunCompleted() => _settingsStore.Update(s => s.Setup.FirstRunCompleted = true);

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
