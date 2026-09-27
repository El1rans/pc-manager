using Microsoft.Extensions.Logging.Abstractions;
using PCManager.Core.Components;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.Core.Tests.Components;

public sealed class ComponentServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeRegistryReader _registryReader = new();
    private readonly FakeFileSystem _fileSystem = new();
    private readonly FakeProcessProbe _processProbe = new();
    private readonly FakeProcessRunner _processRunner = new();
    private readonly FakeElevationService _elevationService = new();

    public ComponentServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsStore = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ComponentService CreateService() =>
        new(_registryReader, _fileSystem, _processProbe, _processRunner, _settingsStore, _elevationService, NullLogger<ComponentService>.Instance);

    // ---------------------------------------------------------------- detection

    [Fact]
    public async Task GetStatusAsync_NothingFound_ReturnsNotInstalled()
    {
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.AnyDesk, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.NotInstalled, status.State);
    }

    [Fact]
    public async Task GetStatusAsync_UninstallEntryAndExeFound_ReturnsInstalledWithVersionAndPath()
    {
        const string exePath = @"C:\Program Files\AnyDesk\AnyDesk.exe";
        _registryReader.SetUninstallEntry("AnyDesk", new UninstallEntry("9.7.16", null));
        _fileSystem.AddFile(exePath);
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.AnyDesk, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Equal("9.7.16", status.Version);
        Assert.Equal(exePath, status.Path);
        Assert.True(status.PathIsTrusted);
    }

    [Fact]
    public async Task GetStatusAsync_HkcuUninstallEntry_ReturnsPathNotTrusted()
    {
        // A per-user (HKCU) install location is writable by the current user, not just an
        // administrator - callers that would execute this path while elevated (see
        // AnyDeskService.BuildStateAsync) must not trust it.
        const string installLocation = @"C:\Users\parent\AppData\Local\AnyDesk";
        _registryReader.SetUninstallEntry("AnyDesk", new UninstallEntry("9.7.16", installLocation, IsPerMachine: false));
        _fileSystem.AddFile(Path.Combine(installLocation, "AnyDesk.exe"));
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.AnyDesk, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.False(status.PathIsTrusted);
    }

    [Fact]
    public async Task GetStatusAsync_ExeFoundUnderUninstallInstallLocation_ReturnsInstalled()
    {
        _registryReader.SetUninstallEntry("AnyDesk", new UninstallEntry("9.7.16", @"D:\Apps\AnyDesk"));
        _fileSystem.AddFile(@"D:\Apps\AnyDesk\AnyDesk.exe");
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.AnyDesk, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Equal(@"D:\Apps\AnyDesk\AnyDesk.exe", status.Path);
    }

    [Fact]
    public async Task GetStatusAsync_ProcessRunning_ReturnsRunning()
    {
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", null));
        _fileSystem.AddFile(@"C:\Program Files\OpenRGB\OpenRGB.exe");
        _processProbe.SetRunning("OpenRGB");
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
    }

    [Fact]
    public async Task GetStatusAsync_OpenRgb_UserPathOverride_IsUsedWhenPresent()
    {
        const string overridePath = @"E:\Portable\OpenRGB\OpenRGB.exe";
        _settingsStore.Update(s => s.Lighting.OpenRgbPathOverride = overridePath);
        _fileSystem.AddFile(overridePath);
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Equal(overridePath, status.Path);
        Assert.False(status.PathIsTrusted);
    }

    [Fact]
    public async Task GetStatusAsync_PawnIo_UninstallEntryButNoService_ReturnsNotInstalled()
    {
        // The setup app can be recorded as "installed" even though the driver failed to register -
        // the spec requires both signals before treating PawnIO as installed.
        _registryReader.SetUninstallEntry("PawnIO", new UninstallEntry("2.2.0", null));
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.PawnIo, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.NotInstalled, status.State);
    }

    [Fact]
    public async Task GetStatusAsync_PawnIo_UninstallEntryAndService_ReturnsInstalled()
    {
        _registryReader.SetUninstallEntry("PawnIO", new UninstallEntry("2.2.0", null));
        _registryReader.SetServiceExists("PawnIO");
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.PawnIo, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Equal("2.2.0", status.Version);
    }

    [Fact]
    public async Task GetStatusAsync_PawnIo_ServiceOnlyNoUninstallEntry_ReturnsNotInstalled()
    {
        _registryReader.SetServiceExists("PawnIO");
        var service = CreateService();

        var status = await service.GetStatusAsync(ComponentIds.PawnIo, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.NotInstalled, status.State);
    }

    // ---------------------------------------------------------------- install

    [Fact]
    public async Task InstallAsync_Success_UsesExpectedWingetArguments()
    {
        _processRunner.NextExitCode = 0;
        var service = CreateService();

        await service.InstallAsync(
            ComponentIds.AnyDesk, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        var call = Assert.Single(_processRunner.RunCalls);
        Assert.Equal("winget", call.FileName);
        Assert.Equal(
            ["install", "--id", "AnyDesk.AnyDesk", "--exact", "--silent",
                "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
            call.Arguments);
    }

    [Fact]
    public async Task InstallAsync_ExitZero_ReturnsRedetectedStatusAndRaisesStatusChanged()
    {
        const string exePath = @"C:\Program Files\AnyDesk\AnyDesk.exe";
        _registryReader.SetUninstallEntry("AnyDesk", new UninstallEntry("9.7.16", null));
        _fileSystem.AddFile(exePath);
        _processRunner.NextExitCode = 0;
        var service = CreateService();
        ComponentStatusChangeEventInfo? raised = null;
        service.StatusChanged += (_, e) => raised = e;

        var status = await service.InstallAsync(
            ComponentIds.AnyDesk, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.NotNull(raised);
        Assert.Equal(ComponentIds.AnyDesk, raised!.ComponentId);
        Assert.Equal(ComponentState.Installed, raised.Status.State);
    }

    [Theory]
    [InlineData(unchecked((int)0x8A150061))] // APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED
    [InlineData(unchecked((int)0x8A15002B))] // APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE
    [InlineData(unchecked((int)0x8A15010D))] // APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED
    public async Task InstallAsync_AlreadyInstalledExitCodes_CountAsSuccess(int exitCode)
    {
        _registryReader.SetUninstallEntry("AnyDesk", new UninstallEntry("9.7.16", null));
        _fileSystem.AddFile(@"C:\Program Files\AnyDesk\AnyDesk.exe");
        _processRunner.NextExitCode = exitCode;
        var service = CreateService();

        var status = await service.InstallAsync(
            ComponentIds.AnyDesk, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
    }

    [Theory]
    [InlineData(unchecked((int)0x8A15010C))] // APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER
    [InlineData(unchecked((int)0x800704C7))] // HRESULT_FROM_WIN32(ERROR_CANCELLED) - PawnIO's likely real code
    [InlineData(1223)] // ERROR_CANCELLED as a raw exit code
    public async Task InstallAsync_UserCancelledExitCodes_ReturnClearErrorMessage(int exitCode)
    {
        _processRunner.NextExitCode = exitCode;
        var service = CreateService();

        var status = await service.InstallAsync(
            ComponentIds.PawnIo, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Error, status.State);
        Assert.Equal("Installation was cancelled.", status.Message);
    }

    [Fact]
    public async Task InstallAsync_RebootRequiredExitCode_CountsAsSuccessWithRestartMessage()
    {
        _registryReader.SetUninstallEntry("PawnIO", new UninstallEntry("2.2.0", null));
        _registryReader.SetServiceExists("PawnIO");
        _processRunner.NextExitCode = unchecked((int)0x8A150109); // APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_TO_FINISH
        var service = CreateService();

        var status = await service.InstallAsync(
            ComponentIds.PawnIo, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Equal("Restart your PC to finish setup.", status.Message);
    }

    [Fact]
    public async Task InstallAsync_UnknownFailureExitCode_ReturnsReadableMessage()
    {
        _processRunner.NextExitCode = unchecked((int)0x8A150001); // APPINSTALLER_CLI_ERROR_INTERNAL_ERROR
        var service = CreateService();

        var status = await service.InstallAsync(
            ComponentIds.OpenRgb, NullProgress, NullProgress, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Error, status.State);
        Assert.Contains("8A150001", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_OnceLaunched_PassesCancellationTokenNoneToProcessRunner()
    {
        _processRunner.NextExitCode = 0;
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.InstallAsync(ComponentIds.AnyDesk, NullProgress, NullProgress, cts.Token);

        var tokenPassedToWinget = Assert.Single(_processRunner.RunCancellationTokens);
        Assert.False(tokenPassedToWinget.CanBeCanceled);
    }

    [Fact]
    public async Task InstallAsync_TokenAlreadyCancelled_NeverLaunchesWinget()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.InstallAsync(ComponentIds.AnyDesk, NullProgress, NullProgress, cts.Token));

        Assert.Empty(_processRunner.RunCalls);
    }

    // ---------------------------------------------------------------- start

    [Fact]
    public async Task StartAsync_InstalledNotRunning_StartsWithDefinitionArgumentsAndReportsRunning()
    {
        const string exePath = @"C:\Program Files\OpenRGB\OpenRGB.exe";
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", null));
        _fileSystem.AddFile(exePath);
        var service = CreateService();
        ComponentStatusChangeEventInfo? raised = null;
        service.StatusChanged += (_, e) => raised = e;

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
        var call = Assert.Single(_processRunner.StartDetachedCalls);
        Assert.Equal(exePath, call.FileName);
        Assert.Equal(["--server", "--startminimized"], call.Arguments);
        Assert.NotNull(raised);
        Assert.Equal(ComponentState.Running, raised!.Status.State);
    }

    [Fact]
    public async Task StartAsync_NotInstalled_DoesNotStartAnything()
    {
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.NotInstalled, status.State);
        Assert.Empty(_processRunner.StartDetachedCalls);
    }

    [Fact]
    public async Task StartAsync_ComponentWithNoStartNotion_IsNoOp()
    {
        _registryReader.SetUninstallEntry("PawnIO", new UninstallEntry("2.2.0", null));
        _registryReader.SetServiceExists("PawnIO");
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.PawnIo, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Installed, status.State);
        Assert.Empty(_processRunner.StartDetachedCalls);
    }

    [Fact]
    public async Task StartAsync_AlreadyRunning_DoesNotStartAgain()
    {
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", null));
        _fileSystem.AddFile(@"C:\Program Files\OpenRGB\OpenRGB.exe");
        _processProbe.SetRunning("OpenRGB");
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
        Assert.Empty(_processRunner.StartDetachedCalls);
    }

    // ---------------------------------------------------------------- start + elevation safety

    [Fact]
    public async Task StartAsync_ElevatedWithSettingsOverridePath_RefusesAndDoesNotStart()
    {
        const string overridePath = @"E:\Portable\OpenRGB\OpenRGB.exe";
        _settingsStore.Update(s => s.Lighting.OpenRgbPathOverride = overridePath);
        _fileSystem.AddFile(overridePath);
        _elevationService.IsElevated = true;
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Empty(_processRunner.StartDetachedCalls);
        Assert.Contains("administrator", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_ElevatedWithHkcuUninstallEntry_RefusesAndDoesNotStart()
    {
        const string exePath = @"C:\Users\someone\AppData\Local\OpenRGB\OpenRGB.exe";
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", @"C:\Users\someone\AppData\Local\OpenRGB", IsPerMachine: false));
        _fileSystem.AddFile(exePath);
        _elevationService.IsElevated = true;
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Empty(_processRunner.StartDetachedCalls);
        Assert.Contains("administrator", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_ElevatedWithProgramFilesPath_StartsNormally()
    {
        const string exePath = @"C:\Program Files\OpenRGB\OpenRGB.exe";
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", null));
        _fileSystem.AddFile(exePath);
        _elevationService.IsElevated = true;
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
        Assert.Single(_processRunner.StartDetachedCalls);
    }

    [Fact]
    public async Task StartAsync_ElevatedWithHklmUninstallEntry_StartsNormally()
    {
        const string exePath = @"D:\Apps\OpenRGB\OpenRGB.exe";
        _registryReader.SetUninstallEntry("OpenRGB", new UninstallEntry("1.0.0", @"D:\Apps\OpenRGB", IsPerMachine: true));
        _fileSystem.AddFile(exePath);
        _elevationService.IsElevated = true;
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
        Assert.Single(_processRunner.StartDetachedCalls);
    }

    [Fact]
    public async Task StartAsync_NotElevatedWithSettingsOverridePath_StartsNormally()
    {
        const string overridePath = @"E:\Portable\OpenRGB\OpenRGB.exe";
        _settingsStore.Update(s => s.Lighting.OpenRgbPathOverride = overridePath);
        _fileSystem.AddFile(overridePath);
        _elevationService.IsElevated = false;
        var service = CreateService();

        var status = await service.StartAsync(ComponentIds.OpenRgb, TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Running, status.State);
        Assert.Single(_processRunner.StartDetachedCalls);
    }

    private static readonly IProgress<string> NullProgress = new NoOpProgress();

    private sealed class NoOpProgress : IProgress<string>
    {
        public void Report(string value)
        {
        }
    }
}
