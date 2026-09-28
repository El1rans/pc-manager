using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.App.Tests.Features.Lighting;

public sealed class DeviceRowViewModelTests
{
    private static RgbDevice CreateDevice(string name = "Keyboard") =>
        new(0, name, RgbDeviceType.Keyboard, "Vendor", [new RgbMode(0, "Direct")], "Direct", 1, []);

    private static (Porchlight.App.Features.Lighting.DeviceRowViewModel Row, FakeLightingService LightingService, List<Porchlight.App.Features.Lighting.DeviceRowViewModel> ExclusionCallbacks)
        CreateRow(RgbDevice? device = null, bool isExcluded = false)
    {
        var lightingService = new FakeLightingService();
        var exclusionCallbacks = new List<Porchlight.App.Features.Lighting.DeviceRowViewModel>();

        var row = new Porchlight.App.Features.Lighting.DeviceRowViewModel(
            device ?? CreateDevice(),
            lightingService,
            () => RgbColor.White,
            NullLogger.Instance,
            isExcluded,
            exclusionCallbacks.Add);

        return (row, lightingService, exclusionCallbacks);
    }

    [Fact]
    public void Constructor_IsExcludedFromCaller_StartsExcludedWithoutInvokingCallback()
    {
        var (row, _, callbacks) = CreateRow(isExcluded: true);

        Assert.True(row.IsExcluded);
        Assert.Empty(callbacks);
    }

    [Fact]
    public void IsExcluded_SetTrue_InvokesCallbackWithSelf()
    {
        var (row, _, callbacks) = CreateRow();

        row.IsExcluded = true;

        var callback = Assert.Single(callbacks);
        Assert.Same(row, callback);
    }

    [Fact]
    public void TogglePicker_FlipsIsPickerOpen()
    {
        var (row, _, _) = CreateRow();

        row.TogglePickerCommand.Execute(null);
        Assert.True(row.IsPickerOpen);

        row.TogglePickerCommand.Execute(null);
        Assert.False(row.IsPickerOpen);
    }

    [Fact]
    public void OnColorPicked_NonFinal_ThrottledToAtMostRateLimit()
    {
        var (row, lightingService, _) = CreateRow();

        // Simulate a burst of drag-move events, far faster than the ~20/s throttle.
        for (var i = 0; i < 50; i++)
        {
            row.OnColorPicked(new RgbColor((byte)i, 0, 0), isFinal: false);
        }

        // Only the very first (rate limiter starts empty) should have gone through synchronously
        // scheduled work; subsequent calls within the same instant are refused by the limiter.
        Assert.True(lightingService.SetDeviceColorCallCount <= 1);
    }

    [Fact]
    public void OnColorPicked_Final_AlwaysApplied()
    {
        var (row, lightingService, _) = CreateRow();
        row.OnColorPicked(new RgbColor(1, 1, 1), isFinal: false);

        row.OnColorPicked(new RgbColor(2, 2, 2), isFinal: true);

        Assert.True(lightingService.SetDeviceColorCallCount >= 1);
    }
}
