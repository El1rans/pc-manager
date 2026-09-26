using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.Core.Components;

namespace PCManager.App.Controls;

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

    public ComponentCardViewModel(
        ComponentDefinition definition,
        IComponentService componentService,
        ILogger<ComponentCardViewModel> logger)
    {
        Definition = definition;
        _componentService = componentService;
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _componentService.StatusChanged += OnComponentServiceStatusChanged;
    }

    public ComponentDefinition Definition { get; }

    public string DisplayName => Definition.DisplayName;

    public string Purpose => Definition.Purpose;

    public ObservableCollection<string> LogLines { get; } = [];

    public bool NeedsStartStep => Definition.StartArguments is not null;

    public bool IsNotInstalled => Status.State == ComponentState.NotInstalled;

    public bool IsInstalledNotRunning => Status.State == ComponentState.Installed;

    public bool IsRunning => Status.State == ComponentState.Running;

    public bool IsError => Status.State == ComponentState.Error;

    public bool ShowPrimaryButton => !IsBusy && Status.State != ComponentState.Running;

    public string StatusText => Status.State switch
    {
        ComponentState.NotInstalled when Definition.RequiresAdmin => "Needs administrator approval",
        ComponentState.NotInstalled => "Not installed",
        ComponentState.Installed when NeedsStartStep => "Installed - not started",
        ComponentState.Installed => "Installed",
        ComponentState.Running => "Ready",
        ComponentState.Error => Status.Message ?? "Something went wrong.",
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
    }

    [RelayCommand(CanExecute = nameof(CanRunPrimaryAction))]
    private async Task PrimaryActionAsync()
    {
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
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(ShowPrimaryButton));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PrimaryButtonText));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowPrimaryButton));

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentServiceStatusChanged;
        _operationCts.Cancel();
        _operationCts.Dispose();
    }
}
