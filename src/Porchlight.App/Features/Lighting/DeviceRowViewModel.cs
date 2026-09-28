using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Lighting;

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

    public DeviceRowViewModel(
        RgbDevice device,
        ILightingService lightingService,
        Func<RgbColor?> getSelectedColor,
        ILogger logger,
        bool isExcluded,
        Action<DeviceRowViewModel> onExclusionChanged)
    {
        Index = device.Index;
        Name = device.Name;
        Glyph = GlyphFor(device.Type);
        Modes = new ObservableCollection<string>(device.Modes.Select(m => m.Name));
        _lightingService = lightingService;
        _getSelectedColor = getSelectedColor;
        _logger = logger;
        _onExclusionChanged = onExclusionChanged;

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

    partial void OnIsExcludedChanged(bool value) => _onExclusionChanged(this);

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
