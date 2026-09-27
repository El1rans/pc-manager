using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.App.Controls;
using PCManager.App.Shell;
using PCManager.Core.Components;
using PCManager.Core.RemoteSupport;
using PCManager.Core.Settings;

namespace PCManager.App.Features.RemoteSupport;

/// <summary>
/// "Get help" page: a non-technical user's two-click path to remote support with AnyDesk. Shows
/// the shared <see cref="ComponentCardViewModel"/> (styled larger) while AnyDesk is not installed
/// or install/detection failed; once installed, shows the address, start/status controls, plain
/// instructions and a safety note instead - see docs/specs/06-remote-support.md.
/// </summary>
public sealed partial class RemoteSupportViewModel : PageViewModelBase, IDisposable
{
    /// <summary>How long the "Copied" confirmation stays visible after a copy button is clicked.</summary>
    private static readonly TimeSpan CopyConfirmationDuration = TimeSpan.FromSeconds(2);

    private readonly IAnyDeskService _anyDeskService;
    private readonly IComponentService _componentService;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<RemoteSupportViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _copyConfirmationCts;

    [ObservableProperty]
    private AnyDeskState? _state;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAnyDeskCommand))]
    private bool _isStarting;

    [ObservableProperty]
    private bool _isAddressCopied;

    [ObservableProperty]
    private bool _isSupportInfoCopied;

    [ObservableProperty]
    private string _helperName;

    /// <summary>Guards <see cref="OnHelperNameChanged"/> against persisting the value right back to
    /// settings while the constructor is only loading it from there.</summary>
    private bool _loadingHelperName;

    public RemoteSupportViewModel(
        IAnyDeskService anyDeskService,
        IComponentService componentService,
        IComponentCardViewModelFactory cardFactory,
        IClipboardService clipboard,
        ISettingsStore settingsStore,
        ILogger<RemoteSupportViewModel> logger)
    {
        _anyDeskService = anyDeskService;
        _componentService = componentService;
        _clipboard = clipboard;
        _settingsStore = settingsStore;
        _logger = logger;
        // Same reasoning as ComponentCardViewModel: always the one UI dispatcher, but still
        // constructible outside a running WPF Application (e.g. unit tests).
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        Card = cardFactory.Create(ComponentIds.AnyDesk);
        Card.PropertyChanged += OnCardPropertyChanged;

        _loadingHelperName = true;
        _helperName = settingsStore.Current.RemoteSupport.HelperName;
        _loadingHelperName = false;

        _componentService.StatusChanged += OnComponentServiceStatusChanged;
    }

    public override string Title => "Get help";

    // Segoe Fluent Icons "People" glyph - visually distinct from the other nav items, per spec.
    public override string Glyph => "";

    // Last in the nav rail - this feature is deliberately the least prominent one.
    public override int Order => 4;

    /// <summary>Card for AnyDesk itself; shown (larger, via the view's own styling) while AnyDesk is
    /// not installed or a detection/install error occurred. See <see cref="ShowComponentCard"/>.</summary>
    public ComponentCardViewModel Card { get; }

    public bool ShowComponentCard => Card.Status.State is ComponentState.NotInstalled or ComponentState.Error;

    public bool ShowMainContent => Card.Status.State is ComponentState.Installed or ComponentState.Running;

    public bool IsRunning => State?.IsRunning ?? false;

    public bool ShowStartButton => ShowMainContent && !IsRunning;

    public string FormattedAddress => AnyDeskIdFormatter.Format(State?.Address);

    public bool HasAddress => !string.IsNullOrEmpty(State?.Address);

    public bool HasHelperName => !string.IsNullOrWhiteSpace(HelperName);

    public string StatusText => IsRunning
        ? "AnyDesk is running - ready for a connection"
        : "AnyDesk is not running";

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        await Card.LoadAsync(cancellationToken).ConfigureAwait(true);
        if (ShowMainContent)
        {
            await RefreshStateAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAnyDesk))]
    private async Task StartAnyDeskAsync()
    {
        IsStarting = true;
        try
        {
            State = await _anyDeskService.LaunchAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigated away mid-launch; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure starting AnyDesk.");
        }
        finally
        {
            IsStarting = false;
        }
    }

    private bool CanStartAnyDesk() => !IsStarting;

    [RelayCommand]
    private void CopyAddress()
    {
        if (State?.Address is not { Length: > 0 } address)
        {
            return;
        }

        _clipboard.SetText(AnyDeskIdFormatter.CopyValue(address));
        ShowCopiedConfirmation(isSupportInfo: false);
    }

    [RelayCommand]
    private void CopySupportInfo()
    {
        var address = FormattedAddress;
        var text = string.Join(
            Environment.NewLine,
            $"Computer: {Environment.MachineName}",
            $"Windows: {Environment.OSVersion.VersionString}",
            $"AnyDesk address: {(address.Length > 0 ? address : "not available yet")}");

        _clipboard.SetText(text);
        ShowCopiedConfirmation(isSupportInfo: true);
    }

    [RelayCommand]
    private async Task RetryAsync(CancellationToken cancellationToken) =>
        await RefreshStateAsync(cancellationToken).ConfigureAwait(true);

    private async Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            State = await _anyDeskService.GetStateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigated away mid-refresh; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure reading AnyDesk state.");
        }
    }

    private void ShowCopiedConfirmation(bool isSupportInfo)
    {
        _copyConfirmationCts?.Cancel();
        _copyConfirmationCts?.Dispose();
        var cts = new CancellationTokenSource();
        _copyConfirmationCts = cts;

        if (isSupportInfo)
        {
            IsSupportInfoCopied = true;
        }
        else
        {
            IsAddressCopied = true;
        }

        _ = ResetCopiedConfirmationAsync(isSupportInfo, cts.Token);
    }

    private async Task ResetCopiedConfirmationAsync(bool isSupportInfo, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CopyConfirmationDuration, cancellationToken).ConfigureAwait(true);
            if (isSupportInfo)
            {
                IsSupportInfoCopied = false;
            }
            else
            {
                IsAddressCopied = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by another copy click; that click's own timer owns the flag now.
        }
    }

    /// <summary>Keeps this page's own view in sync with the shared <see cref="Card"/>'s state
    /// (e.g. detection completing, or AnyDesk finishing an install triggered from this page or
    /// first-run setup) so the address appears without navigating away and back.</summary>
    private void OnCardPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ComponentCardViewModel.Status))
        {
            return;
        }

        OnPropertyChanged(nameof(ShowComponentCard));
        OnPropertyChanged(nameof(ShowMainContent));
        OnPropertyChanged(nameof(ShowStartButton));

        if (ShowMainContent)
        {
            _ = RefreshStateAsync(_lifetimeCts.Token);
        }
        else
        {
            State = null;
        }
    }

    /// <summary>Reacts to AnyDesk being installed/started from elsewhere (another instance of this
    /// page is not possible, but first-run setup shares the same <see cref="IComponentService"/>).</summary>
    private void OnComponentServiceStatusChanged(object? sender, ComponentStatusChangeEventInfo e)
    {
        if (e.ComponentId != ComponentIds.AnyDesk)
        {
            return;
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (ShowMainContent)
            {
                _ = RefreshStateAsync(_lifetimeCts.Token);
            }
        });
    }

    partial void OnHelperNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasHelperName));

        if (_loadingHelperName)
        {
            return;
        }

        _settingsStore.Update(s => s.RemoteSupport.HelperName = value);
    }

    partial void OnStateChanged(AnyDeskState? value)
    {
        OnPropertyChanged(nameof(FormattedAddress));
        OnPropertyChanged(nameof(HasAddress));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ShowStartButton));
        OnPropertyChanged(nameof(StatusText));
    }

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentServiceStatusChanged;
        Card.PropertyChanged -= OnCardPropertyChanged;
        Card.Dispose();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _copyConfirmationCts?.Cancel();
        _copyConfirmationCts?.Dispose();
    }
}
