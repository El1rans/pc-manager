using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Controls;
using Porchlight.App.Shell;
using Porchlight.Core.Components;
using Porchlight.Core.Lighting;
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

    private readonly ILightingService _lightingService;
    private readonly ISettingsStore _settingsStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LightingViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource _cts = new();
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

    public LightingViewModel(
        IComponentCardViewModelFactory componentCardFactory,
        ILightingService lightingService,
        ISettingsStore settingsStore,
        ILoggerFactory loggerFactory)
    {
        _lightingService = lightingService;
        _settingsStore = settingsStore;
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

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
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
            var result = await _lightingService.SetAllColorAsync(scaled, _cts.Token).ConfigureAwait(true);
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
            var result = await _lightingService.TurnOffAllAsync(_cts.Token).ConfigureAwait(true);
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
                return;
            }

            var devices = await _lightingService.GetDevicesAsync(cancellationToken).ConfigureAwait(true);
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(new DeviceRowViewModel(
                    device, _lightingService, GetSelectedColorForDevices, _loggerFactory.CreateLogger<DeviceRowViewModel>()));
            }

            var profiles = await _lightingService.GetProfilesAsync(cancellationToken).ConfigureAwait(true);
            Profiles.Clear();
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            ConnectionStatusText = $"Connected to OpenRGB - {Devices.Count} devices";
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
        });

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
        OpenRgbCard.PropertyChanged -= OnOpenRgbCardPropertyChanged;
        OpenRgbCard.Dispose();
        _lightingService.Disconnected -= OnLightingServiceDisconnected;
        _lightingService.DevicesChanged -= OnLightingServiceDevicesChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
