using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;

namespace Porchlight.App.Controls;

/// <summary>
/// View model behind one <see cref="ComponentCard"/>. Created per component instance via
/// <see cref="IComponentCardViewModelFactory"/> so several pages can each show a card for the same
/// component id without sharing mutable UI state.
/// </summary>
public sealed partial class ComponentCardViewModel : ObservableObject, IDisposable
{
    private readonly IComponentService _componentService;
    private readonly ILogger<ComponentCardViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource _operationCts = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    private ComponentStatus _status = ComponentStatus.NotInstalled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _progressText;

    /// <summary>True until the first <see cref="LoadAsync"/> call completes. Lets a hosting page
    /// avoid flashing "Install" for a component that turns out to already be installed, by showing
    /// a neutral "Checking..." state instead until detection has actually run once.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    private bool _isLoading = true;

    public ComponentCardViewModel(
        ComponentDefinition definition,
        IComponentService componentService,
        ILogger<ComponentCardViewModel> logger)
    {
        Definition = definition;
        _componentService = componentService;
        _logger = logger;
        // Application.Current.Dispatcher (rather than Dispatcher.CurrentDispatcher) is always the
        // one UI dispatcher, regardless of which thread happens to construct this view model. Falls
        // back to the constructing thread's dispatcher when there is no WPF Application (e.g. unit
        // tests), so this view model stays constructible outside a running app.
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _componentService.StatusChanged += OnComponentServiceStatusChanged;
    }

    public ComponentDefinition Definition { get; }

    public string DisplayName => Definition.DisplayName;

    public string Purpose => Definition.Purpose;

    public ObservableCollection<string> LogLines { get; } = [];

    public bool NeedsStartStep => Definition.StartArguments is not null;

    public bool IsNotInstalled => Status.State == ComponentState.NotInstalled;

    /// <summary>Installed and still needs its start step (e.g. OpenRGB before "Start" is clicked).
    /// A component with no start step never reports this - see <see cref="IsReady"/>.</summary>
    public bool IsInstalledNotRunning => Status.State == ComponentState.Installed && NeedsStartStep;

    /// <summary>Nothing left to do: either actually running, or installed with no separate start
    /// step to begin with (e.g. the PawnIO driver).</summary>
    public bool IsReady => Status.State == ComponentState.Running ||
        (Status.State == ComponentState.Installed && !NeedsStartStep);

    public bool IsRunning => Status.State == ComponentState.Running;

    public bool IsError => Status.State == ComponentState.Error;

    public bool ShowPrimaryButton => !IsBusy && !IsLoading && !IsReady;

    public string StatusText => IsLoading ? "Checking..." : Status.Message ?? DefaultStatusText;

    private string DefaultStatusText => Status.State switch
    {
        ComponentState.NotInstalled when Definition.RequiresAdmin => "Needs administrator approval",
        ComponentState.NotInstalled => "Not installed",
        ComponentState.Installed when NeedsStartStep => "Installed - not started",
        ComponentState.Installed => "Ready",
        ComponentState.Running => "Ready",
        ComponentState.Error => "Something went wrong.",
        _ => string.Empty,
    };

    public string PrimaryButtonText => Status.State switch
    {
        ComponentState.Error => "Retry",
        ComponentState.Installed when NeedsStartStep => "Start",
        _ => "Install",
    };

    /// <summary>Loads the component's current status. Safe to call repeatedly (e.g. every time the
    /// hosting page navigates to).</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Status = await _componentService.GetStatusAsync(Definition.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigating away while detection was in flight; expected, not an error.
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunPrimaryAction))]
    private async Task PrimaryActionAsync()
    {
        _operationCts.Cancel();
        _operationCts.Dispose();
        _operationCts = new CancellationTokenSource();
        var cancellationToken = _operationCts.Token;

        IsBusy = true;
        LogLines.Clear();
        ProgressText = null;

        try
        {
            if (Status.State is ComponentState.Installed && NeedsStartStep)
            {
                Status = await _componentService.StartAsync(Definition.Id, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                var log = new Progress<string>(line => LogLines.Add(line));
                var progress = new Progress<string>(text => ProgressText = text);
                // Note: cancelling cancellationToken only prevents InstallAsync from starting
                // winget in the first place - once it has launched, InstallAsync runs it to
                // completion regardless (see IComponentService.InstallAsync).
                Status = await _componentService.InstallAsync(Definition.Id, log, progress, cancellationToken)
                    .ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // The card was unloaded mid-operation; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure installing/starting component {ComponentId}.", Definition.Id);
            Status = new ComponentStatus(ComponentState.Error, Message: "Something went wrong. Check the log and try again.");
        }
        finally
        {
            IsBusy = false;
            ProgressText = null;
        }
    }

    private bool CanRunPrimaryAction() => !IsBusy;

    /// <summary>Keeps this card in sync when the same component is installed/started from
    /// elsewhere (another page's card, or first-run setup).</summary>
    private void OnComponentServiceStatusChanged(object? sender, ComponentStatusChangeEventInfo e)
    {
        if (e.ComponentId != Definition.Id)
        {
            return;
        }

        _dispatcher.InvokeAsync(() => Status = e.Status);
    }

    partial void OnStatusChanged(ComponentStatus value)
    {
        OnPropertyChanged(nameof(IsNotInstalled));
        OnPropertyChanged(nameof(IsInstalledNotRunning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(ShowPrimaryButton));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PrimaryButtonText));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowPrimaryButton));

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPrimaryButton));
        OnPropertyChanged(nameof(StatusText));
    }

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentServiceStatusChanged;
        _operationCts.Cancel();
        _operationCts.Dispose();
    }
}
