using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.App.Tests.Features.Lighting;

public sealed class DeviceRowViewModelTests
{
    private static RgbDevice CreateDevice(string name = "Keyboard", IReadOnlyList<RgbZone>? zones = null) =>
        new(0, name, RgbDeviceType.Keyboard, "Vendor", [new RgbMode(0, "Direct")], "Direct", 1, zones ?? []);

    private static (Porchlight.App.Features.Lighting.DeviceRowViewModel Row, FakeLightingService LightingService, List<Porchlight.App.Features.Lighting.DeviceRowViewModel> ExclusionCallbacks, List<Porchlight.App.Features.Lighting.DeviceRowViewModel> EffectCallbacks)
        CreateRow(RgbDevice? device = null, bool isExcluded = false)
    {
        var lightingService = new FakeLightingService();
        var exclusionCallbacks = new List<Porchlight.App.Features.Lighting.DeviceRowViewModel>();
        var effectCallbacks = new List<Porchlight.App.Features.Lighting.DeviceRowViewModel>();

        var row = new Porchlight.App.Features.Lighting.DeviceRowViewModel(
            device ?? CreateDevice(),
            lightingService,
            () => RgbColor.White,
            NullLogger.Instance,
            isExcluded,
            exclusionCallbacks.Add,
            effectCallbacks.Add);

        return (row, lightingService, exclusionCallbacks, effectCallbacks);
    }

    [Fact]
    public void Constructor_IsExcludedFromCaller_StartsExcludedWithoutInvokingCallback()
    {
        var (row, _, callbacks, _) = CreateRow(isExcluded: true);

        Assert.True(row.IsExcluded);
        Assert.Empty(callbacks);
    }

    [Fact]
    public void IsExcluded_SetTrue_InvokesCallbackWithSelf()
    {
        var (row, _, callbacks, _) = CreateRow();

        row.IsExcluded = true;

        var callback = Assert.Single(callbacks);
        Assert.Same(row, callback);
    }

    [Fact]
    public void TogglePicker_FlipsIsPickerOpen()
    {
        var (row, _, _, _) = CreateRow();

        row.TogglePickerCommand.Execute(null);
        Assert.True(row.IsPickerOpen);

        row.TogglePickerCommand.Execute(null);
        Assert.False(row.IsPickerOpen);
    }

    [Fact]
    public void OnColorPicked_NonFinal_ThrottledToAtMostRateLimit()
    {
        var (row, lightingService, _, _) = CreateRow();

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
        var (row, lightingService, _, _) = CreateRow();
        row.OnColorPicked(new RgbColor(1, 1, 1), isFinal: false);

        row.OnColorPicked(new RgbColor(2, 2, 2), isFinal: true);

        Assert.True(lightingService.SetDeviceColorCallCount >= 1);
    }

    [Fact]
    public void AvailableEffects_DeviceWithNoMatrixZone_OmitsMatrixOnlyEffects()
    {
        var device = CreateDevice(zones: [new RgbZone("Main", 5, IsMatrix: false)]);
        var (row, _, _, _) = CreateRow(device);

        Assert.False(row.HasMatrixZone);
        Assert.DoesNotContain("Pac-Man", row.AvailableEffects);
        Assert.DoesNotContain("Rain", row.AvailableEffects);
        Assert.Contains("Rainbow wave", row.AvailableEffects);
        Assert.Contains("None", row.AvailableEffects);
    }

    [Fact]
    public void AvailableEffects_DeviceWithMatrixZone_IncludesMatrixOnlyEffects()
    {
        var device = CreateDevice(zones: [new RgbZone("Keyboard", 117, IsMatrix: true)]);
        var (row, _, _, _) = CreateRow(device);

        Assert.True(row.HasMatrixZone);
        Assert.Contains("Pac-Man", row.AvailableEffects);
        Assert.Contains("Rain", row.AvailableEffects);
    }

    [Fact]
    public void SelectedEffectName_Changed_InvokesEffectCallback()
    {
        var (row, _, _, effectCallbacks) = CreateRow();

        row.SelectedEffectName = "Rainbow wave";

        var callback = Assert.Single(effectCallbacks);
        Assert.Same(row, callback);
    }

    [Fact]
    public void ShowEffectSettings_ReflectsSelectedEffect()
    {
        var (row, _, _, _) = CreateRow();

        Assert.False(row.ShowEffectSettings);

        row.SelectedEffectName = "Breathing";

        Assert.True(row.ShowEffectSettings);
        Assert.True(row.ShowSpeedSetting);
        Assert.True(row.ShowColorSetting);
        Assert.False(row.ShowTemperatureRangeSetting);
    }

    [Fact]
    public void ShowTemperatureRangeSetting_OnlyForCpuTemperature()
    {
        var (row, _, _, _) = CreateRow();

        row.SelectedEffectName = "CPU temperature";

        Assert.True(row.ShowTemperatureRangeSetting);
        Assert.False(row.ShowColorSetting);
        Assert.False(row.ShowSpeedSetting);
    }

    [Fact]
    public void ToEffectAssignment_NoneSelected_ReturnsNull()
    {
        var (row, _, _, _) = CreateRow();

        Assert.Null(row.ToEffectAssignment(showUpdatesAlert: false));
    }

    [Fact]
    public void ToEffectAssignment_BreathingSelected_IncludesSpeedAndColor()
    {
        var (row, _, _, _) = CreateRow();
        row.SelectedEffectName = "Breathing";
        row.EffectSpeed = 2.0;
        row.EffectColorHex = "#00FF00";

        var assignment = row.ToEffectAssignment(showUpdatesAlert: true);

        Assert.NotNull(assignment);
        Assert.Equal("Keyboard", assignment.DeviceKey);
        Assert.Equal("Breathing", assignment.EffectName);
        Assert.True(assignment.ShowUpdatesAlert);
        Assert.Equal("2", assignment.Settings["speed"]);
        Assert.Equal("#00FF00", assignment.Settings["color"]);
    }

    [Fact]
    public void LoadEffectAssignment_RoundTripsThroughToEffectAssignment()
    {
        var device = CreateDevice(zones: [new RgbZone("Keyboard", 117, IsMatrix: true)]);
        var (row, _, _, effectCallbacks) = CreateRow(device);
        row.SelectedEffectName = "Pac-Man";
        row.EffectSpeed = 2.0;
        var savedAssignment = row.ToEffectAssignment(showUpdatesAlert: false);
        effectCallbacks.Clear();

        var (freshRow, _, _, freshCallbacks) = CreateRow(device);
        freshRow.LoadEffectAssignment(savedAssignment);

        Assert.Equal("Pac-Man", freshRow.SelectedEffectName);
        Assert.Equal(2.0, freshRow.EffectSpeed, precision: 6);
        Assert.Empty(freshCallbacks); // Loading must not re-trigger a persist/restart.
    }

    [Fact]
    public void LoadEffectAssignment_Null_SelectsNone()
    {
        var (row, _, _, effectCallbacks) = CreateRow();
        row.SelectedEffectName = "Rainbow wave";
        effectCallbacks.Clear();

        row.LoadEffectAssignment(null);

        Assert.Equal("None", row.SelectedEffectName);
        Assert.Empty(effectCallbacks);
    }
}
