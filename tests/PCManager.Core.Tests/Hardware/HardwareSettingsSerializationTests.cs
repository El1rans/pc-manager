using System.Text.Json;
using System.Text.Json.Serialization;
using PCManager.Core.Hardware;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.Core.Tests.Hardware;

/// <summary>Profile (de)serialization round trip - <see cref="SettingsStore"/> uses these same
/// options (see its private <c>JsonOptions</c>); duplicated here so this test does not depend on
/// that field's accessibility.</summary>
public sealed class HardwareSettingsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void HardwareSettings_RoundTrips_AllFanModes()
    {
        var settings = new HardwareSettings
        {
            FanControlEnabled = true,
            FanControlWarningConfirmed = true,
            MinFanPercent = 35,
            FailsafeTemperatureC = 88,
            FanProfiles = new Dictionary<string, FanProfileSettings>
            {
                ["fan-default"] = new() { Mode = FanMode.Default },
                ["fan-fixed"] = new() { Mode = FanMode.Fixed, FixedPercent = 55 },
                ["fan-curve"] = new()
                {
                    Mode = FanMode.Curve,
                    SourceSensorId = "cpu/0/temperature/0",
                    CurvePoints = [new(30, 30), new(50, 50), new(70, 80), new(90, 100)],
                },
            },
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var restored = JsonSerializer.Deserialize<HardwareSettings>(json, JsonOptions);

        Assert.NotNull(restored);
        Assert.True(restored!.FanControlEnabled);
        Assert.True(restored.FanControlWarningConfirmed);
        Assert.Equal(35, restored.MinFanPercent);
        Assert.Equal(88, restored.FailsafeTemperatureC);
        Assert.Equal(3, restored.FanProfiles.Count);

        Assert.Equal(FanMode.Default, restored.FanProfiles["fan-default"].Mode);

        Assert.Equal(FanMode.Fixed, restored.FanProfiles["fan-fixed"].Mode);
        Assert.Equal(55, restored.FanProfiles["fan-fixed"].FixedPercent);

        var curveProfile = restored.FanProfiles["fan-curve"];
        Assert.Equal(FanMode.Curve, curveProfile.Mode);
        Assert.Equal("cpu/0/temperature/0", curveProfile.SourceSensorId);
        Assert.Equal(4, curveProfile.CurvePoints.Count);
        Assert.Equal(new FanCurvePoint(70, 80), curveProfile.CurvePoints[2]);
    }

    [Fact]
    public void HardwareSettings_Defaults_MatchSpec()
    {
        var settings = new HardwareSettings();

        Assert.False(settings.FanControlEnabled);
        Assert.False(settings.FanControlWarningConfirmed);
        Assert.Equal(FanControlOptions.DefaultMinPercent, settings.MinFanPercent);
        Assert.Equal(FanControlOptions.DefaultFailsafeTemperatureC, settings.FailsafeTemperatureC);
        Assert.Empty(settings.FanProfiles);
    }

    [Fact]
    public void AppSettings_RoundTripsThroughSettingsStore_PreservesHardwareSection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PCManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance, path);
            store.Update(s =>
            {
                s.Hardware.FanControlEnabled = true;
                s.Hardware.FanProfiles["fan1"] = new FanProfileSettings
                {
                    Mode = FanMode.Curve,
                    SourceSensorId = "gpu/0/temperature/0",
                    CurvePoints = [new(40, 30), new(80, 100)],
                };
            });

            var reloaded = new SettingsStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance, path);

            Assert.True(reloaded.Current.Hardware.FanControlEnabled);
            Assert.Equal(FanMode.Curve, reloaded.Current.Hardware.FanProfiles["fan1"].Mode);
            Assert.Equal(2, reloaded.Current.Hardware.FanProfiles["fan1"].CurvePoints.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
