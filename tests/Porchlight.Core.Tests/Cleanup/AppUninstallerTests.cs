using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class AppUninstallerTests
{
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeElevationService _elevation = new();

    private AppUninstaller CreateUninstaller() => new(_runner, _elevation, NullLogger<AppUninstaller>.Instance);

    private static InstalledApp App(bool perMachine, string uninstall = @"""C:\App\unins000.exe"" /x") =>
        new("App", null, null, null, null, uninstall, perMachine);

    [Fact]
    public void StartUninstall_LaunchesTheAppsOwnUninstallerWithoutExtraFlags()
    {
        var result = CreateUninstaller().StartUninstall(App(perMachine: true));

        Assert.Equal(UninstallStartResult.Started, result);
        var call = Assert.Single(_runner.StartDetachedCalls);
        Assert.Equal(@"C:\App\unins000.exe", call.FileName);
        Assert.Equal(["/x"], call.Arguments);
    }

    [Fact]
    public void StartUninstall_PerUserEntryWhileElevated_IsNeverLaunched()
    {
        _elevation.IsElevated = true;
        var uninstaller = CreateUninstaller();
        var app = App(perMachine: false);

        Assert.False(uninstaller.CanStartUninstall(app));
        Assert.Equal(UninstallStartResult.BlockedWhileElevated, uninstaller.StartUninstall(app));
        Assert.Empty(_runner.StartDetachedCalls);
    }

    [Theory]
    [InlineData(true, true)] // Per-machine entry while elevated.
    [InlineData(false, false)] // Per-user entry when not elevated.
    public void StartUninstall_EntryThatMatchesTheElevation_IsLaunched(bool elevated, bool perMachine)
    {
        _elevation.IsElevated = elevated;

        var result = CreateUninstaller().StartUninstall(App(perMachine));

        Assert.Equal(UninstallStartResult.Started, result);
    }
    [Fact]
    public void StartUninstall_UnparseableCommand_DoesNotLaunchAnything()
    {
        var result = CreateUninstaller().StartUninstall(App(perMachine: true, uninstall: "   "));

        Assert.Equal(UninstallStartResult.InvalidCommand, result);
        Assert.Empty(_runner.StartDetachedCalls);
    }
}
