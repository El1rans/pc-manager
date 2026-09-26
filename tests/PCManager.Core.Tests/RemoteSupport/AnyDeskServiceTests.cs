using Microsoft.Extensions.Logging.Abstractions;
using PCManager.Core.Components;
using PCManager.Core.RemoteSupport;
using Xunit;

namespace PCManager.Core.Tests.RemoteSupport;

public sealed class AnyDeskServiceTests
{
    private const string ExePath = @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";

    private readonly FakeComponentService _componentService = new();
    private readonly FakeProcessRunner _processRunner = new();
    private readonly FakeAnyDeskConfigReader _configReader = new();

    private AnyDeskService CreateService() =>
        new(
            _componentService, _processRunner, _configReader, NullLogger<AnyDeskService>.Instance,
            cliCallTimeout: TimeSpan.FromSeconds(5),
            installIdPollTimeout: TimeSpan.FromMilliseconds(200),
            installIdPollInterval: TimeSpan.FromMilliseconds(20));

    [Fact]
    public async Task GetStateAsync_NotInstalled_ReturnsNotInstalledWithNoAddress()
    {
        _componentService.CurrentStatus = ComponentStatus.NotInstalled;
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.False(state.IsInstalled);
        Assert.Null(state.Id);
        Assert.Null(state.Address);
    }

    [Fact]
    public async Task GetStateAsync_CliCallsSucceed_ReadsIdAndAliasFromCli()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, "mom-laptop");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.True(state.IsInstalled);
        Assert.True(state.IsRunning);
        Assert.Equal("123456789", state.Id);
        Assert.Equal("mom-laptop", state.Alias);
        // Alias is preferred as the shown address once set.
        Assert.Equal("mom-laptop", state.Address);
    }

    [Fact]
    public async Task GetStateAsync_NoAliasSet_AddressFallsBackToId()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("123456789", state.Id);
        Assert.Null(state.Alias);
        Assert.Equal("123456789", state.Address);
    }

    [Fact]
    public async Task GetStateAsync_CliFails_FallsBackToConfigFile()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 1);
        _processRunner.SetResult("--get-alias", 1);
        _configReader.SetFile(_configReader.SystemConfPath, "ad.anynet.id=987654321\nad.anynet.alias=\n");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("987654321", state.Id);
    }

    [Fact]
    public async Task GetStateAsync_SystemConfHasNoId_FallsBackToServiceConf()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 1);
        _processRunner.SetResult("--get-alias", 1);
        _configReader.SetFile(_configReader.SystemConfPath, "ad.security.update_channel=main\n");
        _configReader.SetFile(_configReader.ServiceConfPath, "ad.anynet.id=555555555\n");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("555555555", state.Id);
    }

    [Fact]
    public async Task GetStateAsync_NoExePath_ReturnsInstalledWithNoAddressWithoutRunningCli()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", Path: null);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.True(state.IsInstalled);
        Assert.Null(state.Id);
        Assert.Empty(_processRunner.RunArguments);
    }

    [Fact]
    public async Task GetStateAsync_ErrorStatus_ReturnsErrorState()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Error, Message: "boom");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.True(state.IsError);
        Assert.False(state.IsInstalled);
    }

    [Fact]
    public async Task InstallAsync_Fails_ReturnsErrorWithoutPolling()
    {
        _componentService.InstallResult = new ComponentStatus(ComponentState.Error, Message: "Installation failed.");
        var service = CreateService();

        var state = await service.InstallAsync(new Progress<string>(), TestContext.Current.CancellationToken);

        Assert.True(state.IsError);
        Assert.Equal(0, _componentService.StartCallCount);
    }

    [Fact]
    public async Task InstallAsync_InstalledWithNoIdYet_StartsOnceThenPollsUntilIdAppears()
    {
        _componentService.InstallResult = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath);
        // Right after install, AnyDesk has never run, so it has no ID yet; queue that as the first
        // --get-id result (used by the initial BuildStateAsync), then a second poll after it has
        // been started where the ID has registered.
        _processRunner.QueueResults("--get-id", (ExitCode: 1, Line: null), (ExitCode: 0, Line: "123456789"));
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        _componentService.StatusAfterStart = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath);
        var service = CreateService();

        var state = await service.InstallAsync(new Progress<string>(), TestContext.Current.CancellationToken);

        Assert.Equal(1, _componentService.StartCallCount);
        Assert.Equal("123456789", state.Id);
        Assert.True(state.IsInstalled);
    }

    [Fact]
    public async Task InstallAsync_IdNeverAppears_GivesUpAfterPollTimeoutWithoutAnId()
    {
        _componentService.InstallResult = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath);
        _componentService.StatusAfterStart = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 1);
        _processRunner.SetResult("--get-alias", 1);
        var service = CreateService();

        var state = await service.InstallAsync(new Progress<string>(), TestContext.Current.CancellationToken);

        Assert.Null(state.Id);
        Assert.True(state.IsInstalled);
        Assert.Equal(1, _componentService.StartCallCount);
    }

    [Fact]
    public async Task InstallAsync_AlreadyRunningWithId_DoesNotStartAgain()
    {
        _componentService.InstallResult = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.InstallAsync(new Progress<string>(), TestContext.Current.CancellationToken);

        Assert.Equal(0, _componentService.StartCallCount);
        Assert.Equal("123456789", state.Id);
    }

    [Fact]
    public async Task LaunchAsync_DelegatesToComponentServiceStart()
    {
        _componentService.StatusAfterStart = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.LaunchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _componentService.StartCallCount);
        Assert.True(state.IsRunning);
        Assert.Equal("123456789", state.Id);
    }
}
