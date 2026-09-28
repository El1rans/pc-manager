using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Controls;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Components;
using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Lighting;

/// <summary>
/// View model for the Lighting page (see docs/specs/05-lighting.md). Shows the shared
/// <see cref="ComponentCard"/> for OpenRGB until it is installed and running, then the connection
/// header, "All devices" color card, per-device rows, and OpenRGB profiles.
/// </summary>
public sealed partial class LightingViewModel : PageViewModelBase, IDisposable
{
    private const int MaxFavoriteColors = 8;

    private const string NotConnectedMessage =
        "Not connected. In OpenRGB, open SDK Server and click Start Server, then Retry.";

    private const string ActionNotConnectedMessage =
        "Not connected to OpenRGB. Start OpenRGB, then click Retry.";

    /// <summary>Preset swatches shown in the "All devices" card's grid, alongside the hex box.</summary>
    public static readonly IReadOnlyList<string> PresetSwatches =
    [
        "#FFFFFF", "#FF0000", "#FF8000", "#FFFF00", "#00FF00",
        "#00FFFF", "#0080FF", "#8000FF", "#FF00FF", "#000000",
    ];

    /// <summary>How often <see cref="FireAndForgetReconnectAttempt"/> retries while disconnected,
    /// so the page recovers on its own once OpenRGB is reachable again instead of requiring the
    /// user to click Retry - see docs/specs/05-lighting.md addendum, "Auto-reconnect".</summary>
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(10);

    private readonly ILightingService _lightingService;
    private readonly ILightingConflictDetector _conflictDetector;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ISettingsStore _settingsStore;
    private readonly EffectEngine _effectEngine;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LightingViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private readonly ColorApplyRateLimiter _liveApplyRateLimiter = new();
    private CancellationTokenSource _cts = new();
    private Timer? _reconnectTimer;

    /// <summary>Dismissed for this app session only (not persisted) - see
    /// docs/specs/05-lighting.md addendum, "Lighting conflict warning".</summary>
    private bool _conflictsDismissed;

    private bool _disposed;

    /// <summary>Tracks whether <see cref="OpenRgbCard"/> was already ready before the most recent
    /// <see cref="OnNavigatedToAsync"/> call, so that call and the false-&gt;true edge handled by
    /// <see cref="OnOpenRgbCardPropertyChanged"/> never both trigger a refresh for the same
    /// transition (which raced two concurrent <c>ConnectAsync</c> calls - see PR review).</summary>
    private bool _wasOpenRgbCardReady;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Not connected";

    [ObservableProperty]
    private string? _lastActionMessage;

    [ObservableProperty]
    private string _selectedColorHex = "#FFFFFF";

    [ObservableProperty]
    private double _brightnessPercent = 100;

    [ObservableProperty]
    private bool _autoStartOpenRgb;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyToAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(TurnOffAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadProfileCommand))]
    private bool _isBusy;

    /// <summary>Global "Pause effects" toggle (see docs/specs/11-led-effects.md) - session-only,
    /// like <see cref="_conflictsDismissed"/>; effects resume running (if any are assigned) the next
    /// time Porchlight starts.</summary>
    [ObservableProperty]
    private bool _effectsPaused;

    /// <summary>Global "Updates alert overlay" toggle - composed onto every device's assigned
    /// effect via <see cref="EffectRegistry.CreateWithOverlay"/> (see docs/specs/11-led-effects.md);
    /// initialized from whatever was persisted on any one device's assignment, since today it is
    /// applied to all of them together rather than per device.</summary>
    [ObservableProperty]
    private bool _updatesAlertOverlayEnabled;

    public LightingViewModel(
        IComponentCardViewModelFactory componentCardFactory,
        ILightingService lightingService,
        ILightingConflictDetector conflictDetector,
        IUrlLauncher urlLauncher,
        ISettingsStore settingsStore,
        EffectEngine effectEngine,
        ILoggerFactory loggerFactory)
    {
        _lightingService = lightingService;
        _conflictDetector = conflictDetector;
        _urlLauncher = urlLauncher;
        _settingsStore = settingsStore;
        _effectEngine = effectEngine;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LightingViewModel>();
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        OpenRgbCard = componentCardFactory.Create(ComponentIds.OpenRgb);
        OpenRgbCard.PropertyChanged += OnOpenRgbCardPropertyChanged;
        _wasOpenRgbCardReady = OpenRgbCard.IsReady;

        AutoStartOpenRgb = settingsStore.Current.Lighting.AutoStartOpenRgb;
        FavoriteColors = new ObservableCollection<string>(NormalizeFavorites(settingsStore.Current.Lighting.FavoriteColors));

        _lightingService.Disconnected += OnLightingServiceDisconnected;
        _lightingService.DevicesChanged += OnLightingServiceDevicesChanged;
    }

    public override string Title => "Lighting";

    public override string Glyph => "";

    public override int Order => 3;

    /// <summary>The shared setup card for the OpenRGB component (install/start). Shown instead of
    /// the rest of the page until it reports ready.</summary>
    public ComponentCardViewModel OpenRgbCard { get; }

    public bool ShowSetup => !OpenRgbCard.IsReady;

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];

    public ObservableCollection<string> Profiles { get; } = [];

    public ObservableCollection<string> FavoriteColors { get; }

    public bool CanSaveFavorite => FavoriteColors.Count < MaxFavoriteColors;

    /// <summary>Other software detected that can also control an RGB device's lighting (Windows
    /// Dynamic Lighting, vendor RGB apps) - see docs/specs/05-lighting.md addendum, "Lighting
    /// conflict warning". Shown regardless of <see cref="ShowSetup"/>/<see cref="IsConnected"/>:
    /// it's about other software, not about OpenRGB itself.</summary>
    public ObservableCollection<LightingConflictWarning> Conflicts { get; } = [];

    public bool ShowConflicts => !_conflictsDismissed && Conflicts.Count > 0;

    public string PauseEffectsButtonLabel => EffectsPaused ? "Resume effects" : "Pause effects";

    partial void OnEffectsPausedChanged(bool value) => OnPropertyChanged(nameof(PauseEffectsButtonLabel));

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        var conflictsTask = LoadConflictsAsync(cancellationToken);

        var wasReady = OpenRgbCard.IsReady;
        await OpenRgbCard.LoadAsync(cancellationToken).ConfigureAwait(true);

        // A false->true transition here already triggered exactly one RefreshAsync via
        // OnOpenRgbCardPropertyChanged (LoadAsync's own Status assignment raises that
        // synchronously); starting a second one from here would race two concurrent
        // ConnectAsync calls against the same client. Only re-navigating while it was *already*
        // ready needs its own explicit refresh.
        if (wasReady && OpenRgbCard.IsReady)
        {
            await RefreshAsync().ConfigureAwait(true);
        }

        await conflictsTask.ConfigureAwait(true);
    }

    [RelayCommand]
    private void DismissConflicts()
    {
        _conflictsDismissed = true;
        OnPropertyChanged(nameof(ShowConflicts));
    }

    [RelayCommand]
    private void OpenConflictAction(LightingConflictWarning warning)
    {
        if (warning.ActionUri is { Length: > 0 } uri)
        {
            _urlLauncher.Open(uri);
        }
    }

    private async Task LoadConflictsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var warnings = await _conflictDetector.DetectAsync(cancellationToken).ConfigureAwait(true);
            Conflicts.Clear();
            foreach (var warning in warnings)
            {
                Conflicts.Add(warning);
            }

            OnPropertyChanged(nameof(ShowConflicts));
        }
        catch (OperationCanceledException)
        {
            // Navigated away mid-check; expected, not an error.
        }
        catch (Exception ex)
        {
            // Best-effort, informational only - never block the page on this.
            _logger.LogWarning(ex, "Could not check for lighting conflicts.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task Retry()
    {
        await OpenRgbCard.LoadAsync(_cts.Token).ConfigureAwait(true);
        if (OpenRgbCard.IsReady)
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task ApplyToAll()
    {
        if (!RgbColor.TryParse(SelectedColorHex, out var color))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var scaled = color.Scale(BrightnessPercent / 100.0);
            var result = await ApplyColorToAllowedDevicesAsync(scaled, _cts.Token).ConfigureAwait(true);
            LastActionMessage = DescribeFailure(result, "apply the color to");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Apply-to-all was cancelled (page navigated away or retried).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task TurnOffAll()
    {
        IsBusy = true;
        try
        {
            var result = await ApplyColorToAllowedDevicesAsync(RgbColor.Black, _cts.Token).ConfigureAwait(true);
            LastActionMessage = DescribeFailure(result, "turn off");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Turn-off-all was cancelled (page navigated away or retried).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sets <paramref name="color"/> on every device except one the user marked "Don't control this
    /// device" (see <see cref="DeviceRowViewModel.IsExcluded"/> and docs/specs/05-lighting.md
    /// addendum, "Per-device exclusion"). When nothing is excluded this is exactly
    /// <see cref="ILightingService.SetAllColorAsync"/> (the common case, and what
    /// <c>TurnOffAllAsync</c>/<c>SetAllColorAsync</c> are already optimized for); only once at least
    /// one device is excluded does it fall back to setting devices one at a time so the excluded one
    /// can be skipped - OpenRGB's own bulk API has no per-device opt-out.
    /// </summary>
    private async Task<LightingApplyResult> ApplyColorToAllowedDevicesAsync(RgbColor color, CancellationToken cancellationToken)
    {
        if (Devices.Count == 0 || Devices.All(d => !d.IsExcluded))
        {
            // The common case (nothing excluded): OpenRGB's own bulk call, unchanged from before
            // per-device exclusion existed.
            return await _lightingService.SetAllColorAsync(color, cancellationToken).ConfigureAwait(true);
        }

        var succeeded = 0;
        var failed = 0;
        foreach (var device in Devices.Where(d => !d.IsExcluded))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ok = await _lightingService.SetDeviceColorAsync(device.Index, color, cancellationToken).ConfigureAwait(true);
            if (ok)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        return new LightingApplyResult(succeeded, failed);
    }

    private void OnDeviceExclusionChanged(DeviceRowViewModel device) =>
        _settingsStore.Update(s =>
        {
            var names = s.Lighting.ExcludedDeviceNames;
            if (device.IsExcluded)
            {
                if (!names.Contains(device.Name))
                {
                    names.Add(device.Name);
                }
            }
            else
            {
                names.Remove(device.Name);
            }
        });

    /// <summary>Global "Pause effects" button: stops the engine (restoring every running device's
    /// mode) without touching the persisted assignments, so resuming picks the same effects back up
    /// - see docs/specs/11-led-effects.md.</summary>
    [RelayCommand]
    private void ToggleEffectsPaused()
    {
        EffectsPaused = !EffectsPaused;
        if (EffectsPaused)
        {
            _effectEngine.Stop();
        }
        else
        {
            SyncEffectEngine(persist: false);
        }
    }

    partial void OnUpdatesAlertOverlayEnabledChanged(bool value) => SyncEffectEngine(persist: true);

    /// <summary>Called by a <see cref="DeviceRowViewModel"/> whenever its own effect picker/settings
    /// change - persists the new set of assignments and restarts the engine so the change takes
    /// effect immediately (<see cref="EffectEngine.SetAssignments"/> itself only applies on the next
    /// <see cref="EffectEngine.Start"/>). Selecting "None" removes that device's assignment, which
    /// stops it being rendered to and restores its previous mode the next time the engine (re)starts
    /// with the updated list - see docs/specs/11-led-effects.md.</summary>
    private void OnDeviceEffectChanged(DeviceRowViewModel device) => SyncEffectEngine(persist: true);

    /// <summary>Rebuilds the engine's assignments from every device row's current effect selection,
    /// optionally persists them, and restarts the engine (stopped, then started again only if
    /// effects are not paused and at least one device has an effect assigned) so the change is
    /// live immediately.</summary>
    private void SyncEffectEngine(bool persist)
    {
        var assignments = Devices
            .Select(d => d.ToEffectAssignment(UpdatesAlertOverlayEnabled))
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        if (persist)
        {
            _settingsStore.Update(s => s.Lighting.EffectAssignments = assignments);
        }

        _effectEngine.SetAssignments(assignments);
        _effectEngine.Stop();
        if (!EffectsPaused && assignments.Count > 0)
        {
            _effectEngine.Start();
        }
    }

    /// <summary>Applies a color picked from the "All devices" <c>ColorWheelPicker</c> while it is
    /// being dragged (throttled - <paramref name="isFinal"/> false) or once dragging ends
    /// (<paramref name="isFinal"/> true, always applied). The control's own
    /// <see cref="Controls.ColorWheelPicker.SelectedColorHex"/> two-way binding already keeps
    /// <see cref="SelectedColorHex"/> (and so the hex box/favorites) in sync; this only decides
    /// whether/when to push the color to OpenRGB. See docs/specs/05-lighting.md addendum, "Color
    /// wheel picker".</summary>
    public void OnAllDevicesColorPicked(RgbColor color, bool isFinal)
    {
        if (!isFinal && !_liveApplyRateLimiter.TryAcquire())
        {
            return;
        }

        if (isFinal)
        {
            _liveApplyRateLimiter.Reset();
        }

        _ = ApplyLiveColorAsync(color);
    }

    private async Task ApplyLiveColorAsync(RgbColor color)
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            var scaled = color.Scale(BrightnessPercent / 100.0);
            await ApplyColorToAllowedDevicesAsync(scaled, _cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Live color-wheel apply was cancelled (page navigated away or retried).");
        }
    }

    /// <summary>Sets the swatch as the selected color and immediately applies it to every device,
    /// matching the spec's "apply on button press (and on swatch click)".</summary>
    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task SelectSwatch(string hex)
    {
        SelectedColorHex = hex;
        await ApplyToAll().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSaveFavorite))]
    private void SaveFavorite()
    {
        if (!RgbColor.TryParse(SelectedColorHex, out var color))
        {
            return;
        }

        var hex = color.ToHex();
        if (FavoriteColors.Contains(hex))
        {
            return;
        }

        FavoriteColors.Add(hex);
        OnPropertyChanged(nameof(CanSaveFavorite));
        SaveFavoriteCommand.NotifyCanExecuteChanged();
        PersistFavorites();
    }

    [RelayCommand]
    private void RemoveFavorite(string hex)
    {
        if (FavoriteColors.Remove(hex))
        {
            OnPropertyChanged(nameof(CanSaveFavorite));
            SaveFavoriteCommand.NotifyCanExecuteChanged();
            PersistFavorites();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task LoadProfile(string name)
    {
        IsBusy = true;
        try
        {
            var succeeded = await _lightingService.LoadProfileAsync(name, _cts.Token).ConfigureAwait(true);
            LastActionMessage = succeeded ? null : $"Couldn't load profile \"{name}\".";
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Load profile was cancelled (page navigated away or retried).");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnAutoStartOpenRgbChanged(bool value) =>
        _settingsStore.Update(s => s.Lighting.AutoStartOpenRgb = value);

    private bool CanRunCommand() => !IsBusy;

    private RgbColor? GetSelectedColorForDevices()
    {
        if (!RgbColor.TryParse(SelectedColorHex, out var color))
        {
            return null;
        }

        return color.Scale(BrightnessPercent / 100.0);
    }

    private static string? DescribeFailure(LightingApplyResult result, string verb)
    {
        if (result == LightingApplyResult.NotConnected)
        {
            return ActionNotConnectedMessage;
        }

        if (result.FailedCount == 0)
        {
            return null;
        }

        return result.SucceededCount == 0
            ? $"Couldn't {verb} any device."
            : $"Couldn't {verb} {result.FailedCount} device(s).";
    }

    private static List<string> NormalizeFavorites(IEnumerable<string> favorites)
    {
        var normalized = new List<string>();
        foreach (var favorite in favorites)
        {
            if (RgbColor.TryParse(favorite, out var color))
            {
                var hex = color.ToHex();
                if (!normalized.Contains(hex))
                {
                    normalized.Add(hex);
                }
            }

            if (normalized.Count >= MaxFavoriteColors)
            {
                break;
            }
        }

        return normalized;
    }

    private async Task RefreshAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;

        IsBusy = true;
        try
        {
            var connected = await _lightingService.ConnectAsync(cancellationToken).ConfigureAwait(true);
            IsConnected = connected;

            if (!connected)
            {
                ConnectionStatusText = NotConnectedMessage;
                Devices.Clear();
                Profiles.Clear();
                StartReconnectTimer();
                return;
            }

            var excludedDeviceNames = _settingsStore.Current.Lighting.ExcludedDeviceNames;
            var effectAssignments = _settingsStore.Current.Lighting.EffectAssignments;
            var devices = await _lightingService.GetDevicesAsync(cancellationToken).ConfigureAwait(true);
            Devices.Clear();
            foreach (var device in devices)
            {
                var row = new DeviceRowViewModel(
                    device,
                    _lightingService,
                    GetSelectedColorForDevices,
                    _loggerFactory.CreateLogger<DeviceRowViewModel>(),
                    isExcluded: excludedDeviceNames.Contains(device.Name),
                    onExclusionChanged: OnDeviceExclusionChanged,
                    onEffectChanged: OnDeviceEffectChanged);

                var assignment = effectAssignments.FirstOrDefault(
                    a => string.Equals(a.DeviceKey, device.Name, StringComparison.OrdinalIgnoreCase));
                row.LoadEffectAssignment(assignment);
                Devices.Add(row);
            }

            if (effectAssignments.Count > 0)
            {
                // Setting the property (rather than the backing field) re-syncs the engine via
                // OnUpdatesAlertOverlayEnabledChanged, which is redundant with the explicit
                // SyncEffectEngine call just below but harmless - it only re-sends the same,
                // just-loaded assignments.
                UpdatesAlertOverlayEnabled = effectAssignments.Any(a => a.ShowUpdatesAlert);
            }

            SyncEffectEngine(persist: false);

            var profiles = await _lightingService.GetProfilesAsync(cancellationToken).ConfigureAwait(true);
            Profiles.Clear();
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            ConnectionStatusText = $"Connected to OpenRGB - {Devices.Count} devices";
            StopReconnectTimer();
        }
        catch (OperationCanceledException)
        {
            // Navigated away or retried again mid-refresh; expected, not an error.
        }
        catch (Exception ex)
        {
            // ILightingService itself never throws (see its own doc comment); this guards against
            // a bug there so the page shows "not connected" instead of an unhandled exception.
            _logger.LogError(ex, "Unexpected failure refreshing the Lighting page.");
            IsConnected = false;
            ConnectionStatusText = NotConnectedMessage;
            StartReconnectTimer();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistFavorites() =>
        _settingsStore.Update(s => s.Lighting.FavoriteColors = FavoriteColors.ToList());

    private void OnOpenRgbCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ComponentCardViewModel.IsReady))
        {
            return;
        }

        OnPropertyChanged(nameof(ShowSetup));

        var isReadyNow = OpenRgbCard.IsReady;
        if (isReadyNow && !_wasOpenRgbCardReady)
        {
            // The false->true edge - the only transition that should kick off a refresh from here;
            // OnNavigatedToAsync handles the "was already ready" case itself so the two never both
            // fire for the same becoming-ready moment.
            _ = RefreshAsync();
        }

        _wasOpenRgbCardReady = isReadyNow;
    }

    private void OnLightingServiceDisconnected(object? sender, EventArgs e) =>
        _dispatcher.InvokeAsync(() =>
        {
            IsConnected = false;
            ConnectionStatusText = NotConnectedMessage;
            StartReconnectTimer();
        });

    /// <summary>Starts (if not already running) a timer that retries the connection every
    /// <see cref="ReconnectInterval"/> while disconnected, so the page recovers on its own once
    /// OpenRGB is reachable again - see docs/specs/05-lighting.md addendum, "Auto-reconnect".
    /// Stopped by <see cref="StopReconnectTimer"/> as soon as a connection succeeds again, or by
    /// <see cref="Dispose"/>.</summary>
    private void StartReconnectTimer() =>
        _reconnectTimer ??= new Timer(_ => FireAndForgetReconnectAttempt(), null, ReconnectInterval, ReconnectInterval);

    private void StopReconnectTimer()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }

    private void FireAndForgetReconnectAttempt()
    {
        if (_disposed)
        {
            return;
        }

        _ = _dispatcher.InvokeAsync(async () =>
        {
            if (_disposed || IsConnected)
            {
                return;
            }

            try
            {
                await OpenRgbCard.LoadAsync(_cts.Token).ConfigureAwait(true);
                if (OpenRgbCard.IsReady)
                {
                    await RefreshAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                // Page navigated away or another refresh started; the next timer tick tries again.
            }
            catch (ObjectDisposedException)
            {
                // Raced Dispose(); nothing left to reconnect for.
            }
        });
    }

    private void OnLightingServiceDevicesChanged(object? sender, EventArgs e) =>
        _dispatcher.InvokeAsync(async () =>
        {
            if (IsConnected)
            {
                await RefreshAsync().ConfigureAwait(true);
            }
        });

    /// <summary>Idempotent: page view models are disposed twice on host shutdown (see
    /// <see cref="Shell.PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/>), and a second
    /// <see cref="CancellationTokenSource.Cancel()"/> on a disposed source would throw.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopReconnectTimer();
        OpenRgbCard.PropertyChanged -= OnOpenRgbCardPropertyChanged;
        OpenRgbCard.Dispose();
        _lightingService.Disconnected -= OnLightingServiceDisconnected;
        _lightingService.DevicesChanged -= OnLightingServiceDevicesChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
