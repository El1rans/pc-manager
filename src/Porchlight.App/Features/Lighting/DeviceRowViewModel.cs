using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;

namespace Porchlight.App.Features.Lighting;

/// <summary>
/// One row in the Lighting page's device list: an icon, the device's name, a mode combo box, and a
/// swatch button that applies the "All devices" card's currently selected color to this device
/// only. See 05-lighting.md, "Device list".
/// </summary>
public sealed partial class DeviceRowViewModel : ObservableObject
{
    private readonly ILightingService _lightingService;
    private readonly Func<RgbColor?> _getSelectedColor;
    private readonly ILogger _logger;

    /// <summary>Set while applying <see cref="SyncSelectedModeAfterColorApplied"/>'s own update, so
    /// it does not loop back into <see cref="OnSelectedModeChanged"/> and re-send the mode OpenRGB
    /// was just told to use.</summary>
    private bool _suppressModeChangeHandler;

    /// <summary>Throttles live pushes to OpenRGB while the per-device <c>ColorWheelPicker</c>
    /// popup is being dragged (see <see cref="OnColorPicked"/> and
    /// docs/specs/05-lighting.md addendum) so a fast drag does not flood the SDK connection.</summary>
    private readonly ColorApplyRateLimiter _liveApplyRateLimiter = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetColorCommand))]
    private string _selectedMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetColorCommand))]
    private bool _isBusy;

    /// <summary>The color currently shown in this device's own <c>ColorWheelPicker</c> popup
    /// (<see cref="IsPickerOpen"/>) - independent of the page's shared "All devices" color.</summary>
    [ObservableProperty]
    private string _pickerColorHex = "#FFFFFF";

    [ObservableProperty]
    private bool _isPickerOpen;

    /// <summary>"Don't control this device" (see docs/specs/05-lighting.md addendum, "Per-device
    /// exclusion"): persisted via <see cref="_onExclusionChanged"/>. Excluded from "Apply to
    /// all"/"Turn off all"; still settable individually.</summary>
    [ObservableProperty]
    private bool _isExcluded;

    private readonly Action<DeviceRowViewModel> _onExclusionChanged;

    private readonly Action<DeviceRowViewModel> _onEffectChanged;

    /// <summary>Set while <see cref="LoadEffectAssignment"/> is applying a persisted assignment, so
    /// its own property writes do not loop back into <see cref="_onEffectChanged"/> and re-persist
    /// what was just loaded (same reasoning as <see cref="_suppressModeChangeHandler"/>).</summary>
    private bool _suppressEffectChangeHandler;

    /// <summary>"None (device mode)" plus every effect offered by the LED effects UI (see
    /// docs/specs/11-led-effects.md) - a curated subset of <see cref="EffectRegistry.Names"/>: it
    /// leaves out "Typing ripple" since <c>IKeyPressSource</c> has no real implementation yet (a
    /// no-op source is wired so the effect would just never animate), see the phase-2 note in the
    /// spec.</summary>
    public const string NoneEffectName = "None";

    private static readonly IReadOnlyList<string> AllEffectNames =
        [NoneEffectName, "Rainbow wave", "Breathing", "CPU temperature", "Pac-Man", "Rain"];

    /// <summary>Effects that only render on a matrix (2D grid) zone - see <see cref="PacManEffect"/>
    /// and <see cref="RainEffect"/>'s own doc comments ("Matrix-only: on a non-matrix layout this
    /// renders nothing"). Not offered for a device with no matrix zone.</summary>
    private static readonly HashSet<string> MatrixOnlyEffectNames =
        new(["Pac-Man", "Rain"], StringComparer.Ordinal);

    [ObservableProperty]
    private string _selectedEffectName = NoneEffectName;

    /// <summary>A generic 0.25x-3x speed multiplier, applied to whichever effect-specific rate
    /// setting the selected effect actually uses (see <see cref="ToEffectAssignment"/>) - kept as
    /// one slider in the UI rather than one differently-named slider per effect.</summary>
    [ObservableProperty]
    private double _effectSpeed = 1.0;

    /// <summary>The breathing color, via the shared <c>ColorWheelPicker</c>.</summary>
    [ObservableProperty]
    private string _effectColorHex = "#FFFFFF";

    [ObservableProperty]
    private double _effectMinTempC = 30;

    [ObservableProperty]
    private double _effectMaxTempC = 85;

    public DeviceRowViewModel(
        RgbDevice device,
        ILightingService lightingService,
        Func<RgbColor?> getSelectedColor,
        ILogger logger,
        bool isExcluded,
        Action<DeviceRowViewModel> onExclusionChanged,
        Action<DeviceRowViewModel> onEffectChanged)
    {
        Index = device.Index;
        Name = device.Name;
        Glyph = GlyphFor(device.Type);
        Modes = new ObservableCollection<string>(device.Modes.Select(m => m.Name));
        HasMatrixZone = device.Zones.Any(z => z.IsMatrix);
        AvailableEffects = new ObservableCollection<string>(
            HasMatrixZone ? AllEffectNames : AllEffectNames.Where(n => !MatrixOnlyEffectNames.Contains(n)));
        _lightingService = lightingService;
        _getSelectedColor = getSelectedColor;
        _logger = logger;
        _onExclusionChanged = onExclusionChanged;
        _onEffectChanged = onEffectChanged;

        // Assigning the backing field directly (not the property) so the mode combo box starts on
        // the device's actual active mode, and the exclusion checkbox on its saved state, without
        // immediately re-sending/re-persisting either as a "change".
        _selectedMode = device.ActiveMode;
        _isExcluded = isExcluded;
    }

    public int Index { get; }

    public string Name { get; }

    public string Glyph { get; }

    public ObservableCollection<string> Modes { get; }

    /// <summary>Whether this device has a matrix (2D grid) zone - e.g. a keyboard's per-key
    /// layout - gating which effects <see cref="AvailableEffects"/> offers.</summary>
    public bool HasMatrixZone { get; }

    /// <summary>Effect names this device's picker offers: <see cref="AllEffectNames"/>, minus the
    /// matrix-only ones when <see cref="HasMatrixZone"/> is false.</summary>
    public ObservableCollection<string> AvailableEffects { get; }

    public bool ShowEffectSettings => SelectedEffectName != NoneEffectName;

    public bool ShowSpeedSetting => SelectedEffectName is "Rainbow wave" or "Breathing" or "Pac-Man" or "Rain";

    public bool ShowColorSetting => SelectedEffectName == "Breathing";

    public bool ShowTemperatureRangeSetting => SelectedEffectName == "CPU temperature";

    partial void OnIsExcludedChanged(bool value) => _onExclusionChanged(this);

    partial void OnSelectedEffectNameChanged(string value)
    {
        OnPropertyChanged(nameof(ShowEffectSettings));
        OnPropertyChanged(nameof(ShowSpeedSetting));
        OnPropertyChanged(nameof(ShowColorSetting));
        OnPropertyChanged(nameof(ShowTemperatureRangeSetting));
        RaiseEffectChanged();
    }

    partial void OnEffectSpeedChanged(double value) => RaiseEffectChanged();

    partial void OnEffectColorHexChanged(string value) => RaiseEffectChanged();

    partial void OnEffectMinTempCChanged(double value) => RaiseEffectChanged();

    partial void OnEffectMaxTempCChanged(double value) => RaiseEffectChanged();

    private void RaiseEffectChanged()
    {
        if (!_suppressEffectChangeHandler)
        {
            _onEffectChanged(this);
        }
    }

    /// <summary>Applies a persisted <see cref="EffectAssignment"/> (or null, meaning "None") to this
    /// row's effect properties without re-persisting it or restarting the engine - used when the
    /// Lighting page loads devices, before <c>LightingViewModel</c> does one explicit sync for the
    /// whole page. An assignment naming an effect this device's picker does not offer (a matrix-only
    /// effect saved while a different, matrix-capable device was assigned, or an effect a future
    /// settings-file edit invented) falls back to "None" rather than being shown selected but
    /// unavailable.</summary>
    public void LoadEffectAssignment(EffectAssignment? assignment)
    {
        _suppressEffectChangeHandler = true;
        try
        {
            var effectName = assignment is not null && AvailableEffects.Contains(assignment.EffectName)
                ? assignment.EffectName
                : NoneEffectName;
            SelectedEffectName = effectName;

            var settings = assignment?.Settings ?? new Dictionary<string, string>();
            EffectSpeed = effectName switch
            {
                "Rainbow wave" or "Breathing" => GetDouble(settings, "speed", 1.0),
                "Pac-Man" => GetDouble(settings, "cellsPerSecond", 6.0) / 6.0,
                "Rain" => GetDouble(settings, "rowsPerSecond", 8.0) / 8.0,
                _ => 1.0,
            };
            EffectColorHex = GetString(settings, "color", "#FFFFFF");
            EffectMinTempC = GetDouble(settings, "minC", 30);
            EffectMaxTempC = GetDouble(settings, "maxC", 85);
        }
        finally
        {
            _suppressEffectChangeHandler = false;
        }

        OnPropertyChanged(nameof(ShowEffectSettings));
        OnPropertyChanged(nameof(ShowSpeedSetting));
        OnPropertyChanged(nameof(ShowColorSetting));
        OnPropertyChanged(nameof(ShowTemperatureRangeSetting));
    }

    /// <summary>Builds the <see cref="EffectAssignment"/> to persist/hand to <c>EffectEngine</c> for
    /// this row's current effect selection, or null when <see cref="SelectedEffectName"/> is
    /// "None" (no assignment - the engine then leaves this device on its own mode).</summary>
    public EffectAssignment? ToEffectAssignment(bool showUpdatesAlert)
    {
        if (SelectedEffectName == NoneEffectName)
        {
            return null;
        }

        var settings = new Dictionary<string, string>();
        switch (SelectedEffectName)
        {
            case "Rainbow wave" or "Breathing":
                settings["speed"] = EffectSpeed.ToString(CultureInfo.InvariantCulture);
                if (SelectedEffectName == "Breathing")
                {
                    settings["color"] = EffectColorHex;
                }

                break;

            case "CPU temperature":
                settings["minC"] = EffectMinTempC.ToString(CultureInfo.InvariantCulture);
                settings["maxC"] = EffectMaxTempC.ToString(CultureInfo.InvariantCulture);
                break;

            case "Pac-Man":
                settings["cellsPerSecond"] = (EffectSpeed * 6.0).ToString(CultureInfo.InvariantCulture);
                break;

            case "Rain":
                settings["rowsPerSecond"] = (EffectSpeed * 8.0).ToString(CultureInfo.InvariantCulture);
                break;
        }

        return new EffectAssignment
        {
            DeviceKey = Name,
            EffectName = SelectedEffectName,
            ShowUpdatesAlert = showUpdatesAlert,
            Settings = settings,
        };
    }

    private static double GetDouble(Dictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var raw) &&
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static string GetString(Dictionary<string, string> settings, string key, string fallback) =>
        settings.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;

    [RelayCommand]
    private void TogglePicker() => IsPickerOpen = !IsPickerOpen;

    /// <summary>Applies a color picked from this device's own <c>ColorWheelPicker</c> popup while
    /// dragging (throttled - <paramref name="isFinal"/> false) or once dragging ends
    /// (<paramref name="isFinal"/> true, always applied). See <see cref="_liveApplyRateLimiter"/>
    /// and docs/specs/05-lighting.md addendum, "Color wheel picker".</summary>
    public void OnColorPicked(RgbColor color, bool isFinal)
    {
        if (!isFinal && !_liveApplyRateLimiter.TryAcquire())
        {
            return;
        }

        if (isFinal)
        {
            _liveApplyRateLimiter.Reset();
        }

        _ = ApplyColorAsync(color);
    }

    partial void OnSelectedModeChanged(string value)
    {
        if (_suppressModeChangeHandler)
        {
            return;
        }

        _ = ApplyModeAsync(value);
    }

    [RelayCommand(CanExecute = nameof(CanSetColor))]
    private async Task SetColorAsync()
    {
        var color = _getSelectedColor();
        if (color is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await ApplyColorAsync(color.Value).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSetColor() => !IsBusy;

    private async Task ApplyColorAsync(RgbColor color)
    {
        try
        {
            var succeeded = await _lightingService.SetDeviceColorAsync(Index, color, CancellationToken.None)
                .ConfigureAwait(true);
            if (succeeded)
            {
                // SetDeviceColorAsync may itself have switched the device to "Direct" or "Static"
                // (see LightingModeSelector) - reflect that in the combo box without re-sending it.
                SyncSelectedModeAfterColorApplied();
            }
        }
        catch (OperationCanceledException)
        {
            // The page navigated away mid-call; expected, not an error.
        }
    }

    private void SyncSelectedModeAfterColorApplied()
    {
        var preferred = Modes.FirstOrDefault(m => string.Equals(m, "Direct", StringComparison.OrdinalIgnoreCase))
            ?? Modes.FirstOrDefault(m => string.Equals(m, "Static", StringComparison.OrdinalIgnoreCase));

        if (preferred is not null && !string.Equals(preferred, SelectedMode, StringComparison.OrdinalIgnoreCase))
        {
            _suppressModeChangeHandler = true;
            SelectedMode = preferred;
            _suppressModeChangeHandler = false;
        }
    }

    private async Task ApplyModeAsync(string modeName)
    {
        try
        {
            await _lightingService.SetModeAsync(Index, modeName, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not change mode for device {Device} to {Mode}.", Name, modeName);
        }
    }

    private static string GlyphFor(RgbDeviceType type) => type switch
    {
        RgbDeviceType.Motherboard => "",
        RgbDeviceType.Dram => "",
        RgbDeviceType.Gpu => "",
        RgbDeviceType.Cooler => "",
        RgbDeviceType.LedStrip => "",
        RgbDeviceType.Keyboard => "",
        RgbDeviceType.Mouse => "",
        RgbDeviceType.MouseMat => "",
        RgbDeviceType.Headset => "",
        RgbDeviceType.HeadsetStand => "",
        RgbDeviceType.Gamepad => "",
        RgbDeviceType.Light => "",
        RgbDeviceType.Speaker => "",
        RgbDeviceType.Virtual => "",
        _ => "",
    };
}
