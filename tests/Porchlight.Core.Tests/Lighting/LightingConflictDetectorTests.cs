using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class LightingConflictDetectorTests
{
    private const string DynamicLightingKey = @"Software\Microsoft\Lighting";

    private static LightingConflictDetector CreateDetector(
        out FakeRegistryReader registryReader, out FakeProcessProbe processProbe)
    {
        registryReader = new FakeRegistryReader();
        processProbe = new FakeProcessProbe();
        return new LightingConflictDetector(registryReader, processProbe, NullLogger<LightingConflictDetector>.Instance);
    }

    [Fact]
    public async Task DetectAsync_NothingRunningAndDynamicLightingOff_ReturnsEmpty()
    {
        var detector = CreateDetector(out _, out _);

        var warnings = await detector.DetectAsync(CancellationToken.None);

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task DetectAsync_DynamicLightingEnabled_ReturnsGenericWarning()
    {
        var detector = CreateDetector(out var registryReader, out _);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled", 1);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "Brightness", 75);

        var warnings = await detector.DetectAsync(CancellationToken.None);

        var warning = Assert.Single(warnings);
        Assert.Equal("windows-dynamic-lighting", warning.Id);
        Assert.DoesNotContain("brightness is 0%", warning.Message);
        Assert.Equal("ms-settings:personalization-lighting", warning.ActionUri);
    }

    [Fact]
    public async Task DetectAsync_DynamicLightingEnabledWithZeroBrightness_ReturnsSpecificWarning()
    {
        var detector = CreateDetector(out var registryReader, out _);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled", 1);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "Brightness", 0);

        var warnings = await detector.DetectAsync(CancellationToken.None);

        var warning = Assert.Single(warnings);
        Assert.Contains("brightness is 0%", warning.Message);
    }

    [Fact]
    public async Task DetectAsync_DynamicLightingDisabled_NoWarningEvenIfBrightnessIsZero()
    {
        var detector = CreateDetector(out var registryReader, out _);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled", 0);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "Brightness", 0);

        var warnings = await detector.DetectAsync(CancellationToken.None);

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task DetectAsync_LghubAgentProcessRunning_ReturnsVendorWarning()
    {
        var detector = CreateDetector(out _, out var processProbe);
        processProbe.SetRunning("lghub_agent");

        var warnings = await detector.DetectAsync(CancellationToken.None);

        var warning = Assert.Single(warnings);
        Assert.Equal("vendor-lghub", warning.Id);
        Assert.Contains("Logitech G HUB", warning.Message);
    }

    [Fact]
    public async Task DetectAsync_LogiLamparrayServiceInstalled_ReturnsVendorWarningEvenWithoutProcessRunning()
    {
        var detector = CreateDetector(out var registryReader, out _);
        registryReader.SetServiceExists("logi_lamparray_service");

        var warnings = await detector.DetectAsync(CancellationToken.None);

        var warning = Assert.Single(warnings);
        Assert.Equal("vendor-lghub", warning.Id);
    }

    [Fact]
    public async Task DetectAsync_AsusServicesInstalled_ReturnsSingleArmouryCrateWarning()
    {
        var detector = CreateDetector(out var registryReader, out _);
        registryReader.SetServiceExists("ArmouryCrate.Service");
        registryReader.SetServiceExists("AuraWallpaperService");

        var warnings = await detector.DetectAsync(CancellationToken.None);

        var warning = Assert.Single(warnings);
        Assert.Equal("vendor-asus-armoury-crate", warning.Id);
    }

    [Fact]
    public async Task DetectAsync_MultipleConflicts_ReturnsWindowsDynamicLightingFirst()
    {
        var detector = CreateDetector(out var registryReader, out var processProbe);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled", 1);
        processProbe.SetRunning("iCUE");

        var warnings = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(2, warnings.Count);
        Assert.Equal("windows-dynamic-lighting", warnings[0].Id);
        Assert.Equal("vendor-corsair-icue", warnings[1].Id);
    }

    [Fact]
    public async Task DetectAsync_NeverModifiesRegistryOrStopsProcesses()
    {
        // The detector must be purely read-only (see docs/specs/05-lighting.md addendum) - this is
        // guaranteed structurally by only depending on IRegistryReader/IProcessProbe (which expose
        // no write/kill members at all), but this test documents that intent explicitly.
        var detector = CreateDetector(out var registryReader, out var processProbe);
        registryReader.SetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled", 1);
        processProbe.SetRunning("lghub");

        await detector.DetectAsync(CancellationToken.None);

        // Re-querying returns the same state: nothing was cleared or toggled as a side effect.
        Assert.Equal(1, registryReader.GetCurrentUserDwordValue(DynamicLightingKey, "AmbientLightingEnabled"));
        Assert.True(processProbe.IsRunning("lghub"));
    }
}
