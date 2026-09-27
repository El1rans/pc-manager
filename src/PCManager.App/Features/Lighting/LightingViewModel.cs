using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.App.Controls;
using PCManager.App.Shell;
using PCManager.Core.Components;
using PCManager.Core.Lighting;
using PCManager.Core.Settings;

namespace PCManager.App.Features.Lighting;

/// <summary>
/// View model for the Lighting page (see docs/specs/05-lighting.md). Shows the shared
/// <see cref="ComponentCard"/> for OpenRGB until it is installed and running, then the connection
/// header, "All devices" color card, per-device rows, and OpenRGB profiles.
/// </summary>
public sealed partial class LightingViewModel : PageViewModelBase, IDisposable
{
    private const int MaxFavoriteColors = 8;

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

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Not connected";

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

        AutoStartOpenRgb = settingsStore.Current.Lighting.AutoStartOpenRgb;
        FavoriteColors = new ObservableCollection<string>(settingsStore.Current.Lighting.FavoriteColors);

        _lightingService.Disconnected += OnLightingServiceDisconnected;
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
        await OpenRgbCard.LoadAsync(cancellationToken).ConfigureAwait(true);
        if (OpenRgbCard.IsReady)
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private Task Retry() => RefreshAsync();

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
            await _lightingService.SetAllColorAsync(scaled, _cts.Token).ConfigureAwait(true);
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
            await _lightingService.TurnOffAllAsync(_cts.Token).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectSwatch(string hex) => SelectedColorHex = hex;

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

    [RelayCommand]
    private async Task LoadProfile(string name)
    {
        IsBusy = true;
        try
        {
            await _lightingService.LoadProfileAsync(name, _cts.Token).ConfigureAwait(true);
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
                ConnectionStatusText = "Not connected";
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
            ConnectionStatusText = "Not connected";
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
        if (OpenRgbCard.IsReady)
        {
            _ = RefreshAsync();
        }
    }

    private void OnLightingServiceDisconnected(object? sender, EventArgs e) =>
        _dispatcher.InvokeAsync(() =>
        {
            IsConnected = false;
            ConnectionStatusText = "Not connected";
        });

    public void Dispose()
    {
        OpenRgbCard.PropertyChanged -= OnOpenRgbCardPropertyChanged;
        OpenRgbCard.Dispose();
        _lightingService.Disconnected -= OnLightingServiceDisconnected;
        _cts.Cancel();
        _cts.Dispose();
    }
}
