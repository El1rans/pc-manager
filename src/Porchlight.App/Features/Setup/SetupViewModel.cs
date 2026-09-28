using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Setup;

/// <summary>
/// View model for the first-run ("Choose what to set up") dialog, also reachable later from the
/// sidebar footer's "Set up optional features" button.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject, IDisposable
{
    private readonly IComponentService _componentService;
    private readonly IRegistryReader _registryReader;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<SetupViewModel> _logger;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
    private bool _isBusy;

    /// <summary>True once a "Set up" run has finished at least once (successfully or not) - the
    /// dialog stays open showing per-item results instead of closing immediately. See
    /// <see cref="PrimaryButtonText"/> and <see cref="ShowSkip"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowSkip))]
    private bool _hasRun;

    public SetupViewModel(
        IComponentService componentService,
        IRegistryReader registryReader,
        ISettingsStore settingsStore,
        ILogger<SetupViewModel> logger)
    {
        _componentService = componentService;
        _registryReader = registryReader;
        _settingsStore = settingsStore;
        _logger = logger;
        Items = new ObservableCollection<SetupItemViewModel>(
            ComponentCatalog.All.Select(d => new SetupItemViewModel(d)));
    }

    public ObservableCollection<SetupItemViewModel> Items { get; }

    /// <summary>True if any selected item could still be installed or retried - i.e. is
    /// <see cref="ComponentState.NotInstalled"/> or <see cref="ComponentState.Error"/>.</summary>
    private bool AnyRetryable => Items.Any(
        i => i.IsSelected && i.Status.State is ComponentState.NotInstalled or ComponentState.Error);

    /// <summary>"Set up" before the first run; after that, "Retry" while any selected item is
    /// still not-installed or errored, otherwise "Close".</summary>
    public string PrimaryButtonText => !HasRun ? "Set up" : AnyRetryable ? "Retry" : "Close";

    /// <summary>Hidden once a run has happened - "Close" (via <see cref="PrimaryButtonText"/>)
    /// covers "I'm done here" once results are showing.</summary>
    public bool ShowSkip => !HasRun;

    /// <summary>Raised when the dialog should close (the user chose Skip, or Close after a run).</summary>
    public event EventHandler? CloseRequested;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        // A hint from the Porchlight installer (milestone 07) about which components it already
        // set up - see docs/specs/07-installer.md, "Contract with first-run setup". Detection
        // below is still the source of truth for each item's actual status; this only affects
        // which not-yet-detected-as-installed items default to ticked.
        var installerHandledIds = _registryReader.GetInstallerHandledComponentIds();

        foreach (var item in Items)
        {
            item.Status = await _componentService.GetStatusAsync(item.Definition.Id, cancellationToken)
                .ConfigureAwait(true);

            // Only AnyDesk is pre-ticked by default - it is the one component parents need for
            // remote help to work at all. OpenRGB (cosmetic) and the PawnIO fan driver (installs a
            // kernel driver) are opt-in, so they always start unticked here regardless of install
            // state; see docs/specs/01b-components.md.
            item.IsSelected = item.Definition.Id == ComponentIds.AnyDesk &&
                item.Status.State == ComponentState.NotInstalled &&
                !installerHandledIds.Contains(item.Definition.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunPrimaryAction))]
    private async Task PrimaryActionAsync()
    {
        if (HasRun && !AnyRetryable)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;

        try
        {
            // An item left in Error from a previous attempt is retried the same as one that was
            // never attempted.
            foreach (var item in Items.Where(
                i => i.IsSelected && i.Status.State is ComponentState.NotInstalled or ComponentState.Error))
            {
                item.ProgressMessage = "Installing...";
                var log = new Progress<string>(line => item.ProgressMessage = line);
                var progress = new Progress<string>(text => item.ProgressMessage = text);

                item.Status = await _componentService
                    .InstallAsync(item.Definition.Id, log, progress, _cts.Token)
                    .ConfigureAwait(true);

                item.ProgressMessage = null;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled before any install actually launched (see IComponentService.InstallAsync);
            // expected, not an error.
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
        HasRun = true;
        OnPropertyChanged(nameof(PrimaryButtonText));
    }

    private bool CanRunPrimaryAction() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSkip))]
    private void Skip()
    {
        MarkFirstRunCompleted();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSkip() => !IsBusy;

    /// <summary>Marks first-run as shown regardless of how the dialog is closing (Skip, Close after
    /// a run, or the window's own X button) - see <see cref="SetupWindow"/>'s Closing handler.
    /// Idempotent, so calling it more than once (e.g. Skip already called it, then the window's
    /// Closed handler calls it again) is harmless.</summary>
    public void MarkFirstRunCompleted() => _settingsStore.Update(s => s.Setup.FirstRunCompleted = true);

    /// <summary>Idempotent: <see cref="SetupWindow"/>'s Closed handler disposes this view model, and
    /// the DI container (which tracks transient <see cref="IDisposable"/>s it creates) disposes it
    /// again on host shutdown - cancelling an already-disposed CTS would throw.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
