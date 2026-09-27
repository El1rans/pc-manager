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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetColorCommand))]
    private string _selectedMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetColorCommand))]
    private bool _isBusy;

    public DeviceRowViewModel(
        RgbDevice device, ILightingService lightingService, Func<RgbColor?> getSelectedColor, ILogger logger)
    {
        Index = device.Index;
        Name = device.Name;
        Glyph = GlyphFor(device.Type);
        Modes = new ObservableCollection<string>(device.Modes.Select(m => m.Name));
        _lightingService = lightingService;
        _getSelectedColor = getSelectedColor;
        _logger = logger;

        // Assigning the backing field directly (not the property) so the mode combo box starts on
        // the device's actual active mode without immediately re-sending it as a "change".
        _selectedMode = device.ActiveMode;
    }

    public int Index { get; }

    public string Name { get; }

    public string Glyph { get; }

    public ObservableCollection<string> Modes { get; }

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
            var succeeded = await _lightingService.SetDeviceColorAsync(Index, color.Value, CancellationToken.None)
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
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSetColor() => !IsBusy;

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
