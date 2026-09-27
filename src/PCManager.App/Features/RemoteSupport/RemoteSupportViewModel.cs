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
    /// <summary>How long the "Copied" (or "Couldn't copy") confirmation stays visible after a copy
    /// button is clicked.</summary>
    private static readonly TimeSpan DefaultCopyConfirmationDuration = TimeSpan.FromSeconds(2);

    /// <summary>How often the page polls for AnyDesk's address while it has not registered one
    /// yet (it only does so on first start, so this covers the few seconds after an install or an
    /// explicit "Start AnyDesk").</summary>
    private static readonly TimeSpan DefaultWaitingForIdPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>How often the page re-checks AnyDesk's running status once an address is already
    /// known, so the page notices if AnyDesk is closed while this page is open.</summary>
    private static readonly TimeSpan DefaultRunningStatusPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>How long the page waits for an address to appear before giving up and offering
    /// "Try again" instead of polling forever.</summary>
    private static readonly TimeSpan DefaultIdWaitTimeout = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _copyConfirmationDuration;
    private readonly TimeSpan _waitingForIdPollInterval;
    private readonly TimeSpan _runningStatusPollInterval;
    private readonly TimeSpan _idWaitTimeout;
    private readonly TimeProvider _timeProvider;

    private readonly IAnyDeskService _anyDeskService;
    private readonly IComponentService _componentService;
    private readonly IClipboardService _clipboard;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IWindowsVersionReader _windowsVersionReader;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<RemoteSupportViewModel> _logger;
    private readonly Dispatcher _dispatcher;

    /// <summary>Cancelled and replaced every time the page is (re)navigated to, and cancelled for
    /// good in <see cref="Dispose"/>. Everything this page kicks off in the background - the poll
    /// loop especially - is tied to this, so navigating away or shutting down stops it.</summary>
    private CancellationTokenSource _visitCts = new();

    private readonly CancellationTokenSource _lifetimeCts = new();

    /// <summary>The currently running poll loop, if any - see <see cref="StartPolling"/>.</summary>
    private CancellationTokenSource? _pollCts;

    private DateTime _waitingForIdSince;
    private bool _hasAutoStartedForCurrentWait;

    private CancellationTokenSource? _addressCopyResetCts;
    private CancellationTokenSource? _supportInfoCopyResetCts;

    [ObservableProperty]
    private AnyDeskState? _state;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAnyDeskCommand))]
    private bool _isStarting;

    [ObservableProperty]
    private bool _isAddressCopied;

    [ObservableProperty]
    private bool _addressCopyFailed;

    [ObservableProperty]
    private bool _isSupportInfoCopied;

    [ObservableProperty]
    private bool _supportInfoCopyFailed;

    [ObservableProperty]
    private bool _isAddressTimedOut;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

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
        IUrlLauncher urlLauncher,
        IWindowsVersionReader windowsVersionReader,
        ISettingsStore settingsStore,
        ILogger<RemoteSupportViewModel> logger)
        : this(
            anyDeskService, componentService, cardFactory, clipboard, urlLauncher, windowsVersionReader,
            settingsStore, logger, DefaultCopyConfirmationDuration, DefaultWaitingForIdPollInterval,
            DefaultRunningStatusPollInterval, DefaultIdWaitTimeout, TimeProvider.System)
    {
    }

    /// <summary>Test seam: lets tests use much shorter durations than production so a polling test
    /// does not take tens of seconds to run, and a fake <see cref="TimeProvider"/> (e.g.
    /// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) instead of a real clock/timer, so
    /// the poll loop and copy-confirmation reset never depend on wall-clock timing - see
    /// docs/specs root cause note on the flaky "Get help" tests.</summary>
    public RemoteSupportViewModel(
        IAnyDeskService anyDeskService,
        IComponentService componentService,
        IComponentCardViewModelFactory cardFactory,
        IClipboardService clipboard,
        IUrlLauncher urlLauncher,
        IWindowsVersionReader windowsVersionReader,
        ISettingsStore settingsStore,
        ILogger<RemoteSupportViewModel> logger,
        TimeSpan copyConfirmationDuration,
        TimeSpan waitingForIdPollInterval,
        TimeSpan runningStatusPollInterval,
        TimeSpan idWaitTimeout,
        TimeProvider timeProvider)
    {
        _anyDeskService = anyDeskService;
        _componentService = componentService;
        _clipboard = clipboard;
        _urlLauncher = urlLauncher;
        _windowsVersionReader = windowsVersionReader;
        _settingsStore = settingsStore;
        _logger = logger;
        _copyConfirmationDuration = copyConfirmationDuration;
        _waitingForIdPollInterval = waitingForIdPollInterval;
        _runningStatusPollInterval = runningStatusPollInterval;
        _idWaitTimeout = idWaitTimeout;
        _timeProvider = timeProvider;
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

    // Pinned to the bottom of the nav rail, below every regular page - see IPage.IsPinnedToBottom.
    public override bool IsPinnedToBottom => true;

    // Not used for placement (IsPinnedToBottom handles that); kept only because IPage requires it.
    public override int Order => 0;

    /// <summary>Card for AnyDesk itself; shown (larger, via the view's own styling) while AnyDesk is
    /// not installed or a detection/install error occurred. See <see cref="ShowComponentCard"/>.</summary>
    public ComponentCardViewModel Card { get; }

    public bool ShowComponentCard => Card.Status.State is ComponentState.NotInstalled or ComponentState.Error;

    /// <summary>Shows the "or download it yourself" fallback (e.g. when winget itself could not be
    /// started) alongside the card's own error message and Retry.</summary>
    public bool ShowManualDownloadLink => Card.IsError;

    public bool ShowMainContent => Card.Status.State is ComponentState.Installed or ComponentState.Running;

    /// <summary>Whether a first <see cref="AnyDeskState"/> has actually been loaded yet. Gates the
    /// running/not-running status line so it never flashes "AnyDesk is not running" for an instant
    /// before the real state is known.</summary>
    public bool HasLoadedState => State is not null;

    public bool IsRunning => State?.IsRunning ?? false;

    public bool ShowStartButton => ShowMainContent && HasLoadedState && !IsRunning;

    public string FormattedAddress => AnyDeskIdFormatter.Format(State?.Address);

    public bool HasAddress => !string.IsNullOrEmpty(State?.Address);

    /// <summary>AnyDesk is installed and running (or waiting to register one) but no address has
    /// shown up within <see cref="IdWaitTimeout"/> - see <see cref="TryAgainCommand"/>.</summary>
    public bool ShowAddressTrouble => ShowMainContent && (IsAddressTimedOut || HasError);

    public string AddressTroubleMessage => HasError
        ? ErrorMessage
        : "AnyDesk hasn't got an address yet. Click Start AnyDesk, wait a minute, then try again.";

    public bool HasHelperName => !string.IsNullOrWhiteSpace(HelperName);

    public string HelperLine => $"Your helper: {HelperName}";

    public string Step1Text => HasHelperName ? $"1. Call {HelperName}." : "1. Call the person helping you.";

    public string StatusText => IsRunning
        ? "AnyDesk is running - ready for a connection"
        : "AnyDesk is not running";

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        _visitCts.Cancel();
        _visitCts.Dispose();
        _visitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);

        await Card.LoadAsync(_visitCts.Token).ConfigureAwait(true);

        if (ShowMainContent)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartAnyDesk))]
    private async Task StartAnyDeskAsync()
    {
        IsStarting = true;
        try
        {
            State = await _anyDeskService.LaunchAsync(_visitCts.Token).ConfigureAwait(true);
            HasError = false;
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

        // Re-arm the "waiting for an address" watch: a manual start after it had already given up
        // (ShowAddressTrouble) should get a fresh 60-second budget, not stay stuck on Try again.
        StartPolling();
    }

    private bool CanStartAnyDesk() => !IsStarting;

    [RelayCommand]
    private void CopyAddress()
    {
        if (State?.Address is not { Length: > 0 } address)
        {
            return;
        }

        var success = _clipboard.SetText(AnyDeskIdFormatter.CopyValue(address));
        ShowCopyResult(success, isSupportInfo: false);
    }

    [RelayCommand]
    private void CopySupportInfo()
    {
        var address = FormattedAddress;
        var text = string.Join(
            Environment.NewLine,
            $"Computer: {Environment.MachineName}",
            $"Windows: {_windowsVersionReader.GetFriendlyVersion()}",
            $"AnyDesk address: {(address.Length > 0 ? address : "not available yet")}");

        var success = _clipboard.SetText(text);
        ShowCopyResult(success, isSupportInfo: true);
    }

    [RelayCommand]
    private void DownloadAnyDeskManually() => _urlLauncher.Open("https://anydesk.com/download");

    /// <summary>"Try again" after the page gave up waiting for an address, or after an unexpected
    /// error reading AnyDesk's state - restarts the poll loop with a fresh wait budget.</summary>
    [RelayCommand]
    private void TryAgain() => StartPolling();

    private void StartPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(_visitCts.Token);

        _waitingForIdSince = _timeProvider.GetUtcNow().UtcDateTime;
        _hasAutoStartedForCurrentWait = false;
        IsAddressTimedOut = false;
        HasError = false;

        _ = PollLoopAsync(_pollCts.Token);
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    /// <summary>
    /// Polls <see cref="IAnyDeskService.GetStateAsync"/> on a <see cref="PeriodicTimer"/> for as
    /// long as this page is showing the main content: every <see cref="WaitingForIdPollInterval"/>
    /// while no address is known yet (starting AnyDesk once along the way, since it only registers
    /// an address on first start, and giving up with <see cref="IsAddressTimedOut"/> after
    /// <see cref="IdWaitTimeout"/>), then every <see cref="RunningStatusPollInterval"/> once an
    /// address is known, so the page notices AnyDesk being closed while it is open.
    /// </summary>
    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshStateAsync(cancellationToken).ConfigureAwait(true);

            using var timer = new PeriodicTimer(_waitingForIdPollInterval, _timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true))
            {
                if (!ShowMainContent)
                {
                    return;
                }

                if (State?.Id is null)
                {
                    if (_timeProvider.GetUtcNow().UtcDateTime - _waitingForIdSince >= _idWaitTimeout)
                    {
                        IsAddressTimedOut = true;
                        return;
                    }

                    if (State is { IsRunning: false } && !_hasAutoStartedForCurrentWait)
                    {
                        _hasAutoStartedForCurrentWait = true;
                        await TryAutoStartAsync(cancellationToken).ConfigureAwait(true);
                    }

                    timer.Period = _waitingForIdPollInterval;
                }
                else
                {
                    timer.Period = _runningStatusPollInterval;
                }

                await RefreshStateAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Navigated away, or superseded by a fresh StartPolling() call; expected, not an error.
        }
    }

    private async Task TryAutoStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _anyDeskService.LaunchAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure auto-starting AnyDesk while waiting for its address.");
        }
    }

    private async Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            State = await _anyDeskService.GetStateAsync(cancellationToken).ConfigureAwait(true);
            HasError = false;
        }
        catch (OperationCanceledException)
        {
            // Navigated away mid-refresh, or the poll loop was cancelled; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure reading AnyDesk state.");
            HasError = true;
            ErrorMessage = "Something went wrong getting your AnyDesk address.";
        }
    }

    private void ShowCopyResult(bool success, bool isSupportInfo)
    {
        if (isSupportInfo)
        {
            _supportInfoCopyResetCts?.Cancel();
            _supportInfoCopyResetCts?.Dispose();
            var cts = new CancellationTokenSource();
            _supportInfoCopyResetCts = cts;

            IsSupportInfoCopied = success;
            SupportInfoCopyFailed = !success;
            _ = ResetCopyResultAsync(() => (IsSupportInfoCopied, SupportInfoCopyFailed) = (false, false), cts.Token);
        }
        else
        {
            _addressCopyResetCts?.Cancel();
            _addressCopyResetCts?.Dispose();
            var cts = new CancellationTokenSource();
            _addressCopyResetCts = cts;

            IsAddressCopied = success;
            AddressCopyFailed = !success;
            _ = ResetCopyResultAsync(() => (IsAddressCopied, AddressCopyFailed) = (false, false), cts.Token);
        }
    }

    private async Task ResetCopyResultAsync(Action reset, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_copyConfirmationDuration, _timeProvider, cancellationToken).ConfigureAwait(true);
            reset();
        }
        catch (OperationCanceledException)
        {
            // Superseded by another copy click on the same button; that click's own timer owns the
            // flags now.
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
        OnPropertyChanged(nameof(ShowManualDownloadLink));
        OnPropertyChanged(nameof(ShowMainContent));
        OnPropertyChanged(nameof(ShowStartButton));
        OnPropertyChanged(nameof(ShowAddressTrouble));

        if (ShowMainContent)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
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
                StartPolling();
            }
        });
    }

    partial void OnHelperNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasHelperName));
        OnPropertyChanged(nameof(HelperLine));
        OnPropertyChanged(nameof(Step1Text));

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
        OnPropertyChanged(nameof(HasLoadedState));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ShowStartButton));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnIsAddressTimedOutChanged(bool value) => OnPropertyChanged(nameof(ShowAddressTrouble));

    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(ShowAddressTrouble));

    /// <summary>Sets the helper name from the "Change helper name" dialog's result (called by the
    /// view's code-behind on OK) - the only path that persists it, so an in-progress keystroke is
    /// never saved or shown before the user confirms.</summary>
    public void SetHelperName(string name) => HelperName = name;

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentServiceStatusChanged;
        Card.PropertyChanged -= OnCardPropertyChanged;
        Card.Dispose();
        StopPolling();
        _visitCts.Cancel();
        _visitCts.Dispose();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _addressCopyResetCts?.Cancel();
        _addressCopyResetCts?.Dispose();
        _supportInfoCopyResetCts?.Cancel();
        _supportInfoCopyResetCts?.Dispose();
    }
}
