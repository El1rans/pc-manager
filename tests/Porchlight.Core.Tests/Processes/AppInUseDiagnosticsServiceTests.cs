using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Components;
using Porchlight.Core.Processes;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Processes;

public sealed class AppInUseDiagnosticsServiceTests
{
    private readonly FakeRegistryReader _registryReader = new();
    private readonly FakeAppLockDetector _lockDetector = new();

    private AppInUseDiagnosticsService CreateService() =>
        new(_registryReader, _lockDetector, NullLogger<AppInUseDiagnosticsService>.Instance);

    [Fact]
    public async Task TryDescribeLockingProcessesAsync_KnownInstallLocationWithLocks_ReturnsEnrichedExplanation()
    {
        // Real case: OBS Studio (C:\Program Files\obs-studio) blocked by Chrome and another app
        // holding obs-virtualcam-module64.dll open while OBS itself was not running - see
        // docs/specs/09-friendly-update-outcomes.md's addendum.
        _registryReader.SetUninstallEntry(
            "OBS Studio", new UninstallEntry("30.1.2", @"C:\Program Files\obs-studio", IsPerMachine: true));
        _lockDetector.Names = ["Chrome", "Claude"];

        var result = await CreateService().TryDescribeLockingProcessesAsync("OBSProject.OBSStudio", "OBS Studio");

        Assert.Equal(
            "These programs are using OBS Studio's files: Chrome, Claude. Close them, then try again.", result);
        Assert.Equal(@"C:\Program Files\obs-studio", Assert.Single(_lockDetector.Calls));
    }

    [Fact]
    public async Task TryDescribeLockingProcessesAsync_NoUninstallEntry_ReturnsNull()
    {
        var result = await CreateService().TryDescribeLockingProcessesAsync("Unknown.Id", "Unknown App");

        Assert.Null(result);
        Assert.Empty(_lockDetector.Calls); // never even asks the lock detector without a location
    }

    [Fact]
    public async Task TryDescribeLockingProcessesAsync_UninstallEntryWithNoInstallLocation_ReturnsNull()
    {
        _registryReader.SetUninstallEntry("Some App", new UninstallEntry("1.0", InstallLocation: null));

        var result = await CreateService().TryDescribeLockingProcessesAsync("Some.Id", "Some App");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDescribeLockingProcessesAsync_NoLockingProcessesFound_ReturnsNull()
    {
        // Handle failure gracefully: fall back to the generic text the caller already has.
        _registryReader.SetUninstallEntry("Some App", new UninstallEntry("1.0", @"C:\Program Files\SomeApp"));
        _lockDetector.Names = [];

        var result = await CreateService().TryDescribeLockingProcessesAsync("Some.Id", "Some App");

        Assert.Null(result);
    }
}
