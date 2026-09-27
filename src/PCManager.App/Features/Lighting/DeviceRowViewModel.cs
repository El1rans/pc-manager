using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.Core.Lighting;

namespace PCManager.App.Features.Lighting;

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

    [ObservableProperty]
    private string _selectedMode;

    [ObservableProperty]
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

    partial void OnSelectedModeChanged(string value) => _ = ApplyModeAsync(value);

    [RelayCommand]
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
            await _lightingService.SetDeviceColorAsync(Index, color.Value, CancellationToken.None)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
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
