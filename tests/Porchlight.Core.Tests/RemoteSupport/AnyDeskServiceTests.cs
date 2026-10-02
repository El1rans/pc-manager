using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Components;
using Porchlight.Core.RemoteSupport;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.RemoteSupport;

public sealed class AnyDeskServiceTests
{
    private const string ExePath = @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";
    private const string ValidAlias = "mom-laptop@ad";

    private readonly FakeComponentService _componentService = new();
    private readonly FakeProcessRunner _processRunner = new();
    private readonly FakeAnyDeskConfigReader _configReader = new();
    private readonly FakeElevationService _elevationService = new();

    private AnyDeskService CreateService() =>
        new(
            _componentService, _processRunner, _configReader, _elevationService,
            NullLogger<AnyDeskService>.Instance, cliCallTimeout: TimeSpan.FromMilliseconds(50));

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
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, ValidAlias);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.True(state.IsInstalled);
        Assert.True(state.IsRunning);
        Assert.Equal("123456789", state.Id);
        Assert.Equal(ValidAlias, state.Alias);
        // Alias is preferred as the shown address once set.
        Assert.Equal(ValidAlias, state.Address);
    }

    [Fact]
    public async Task GetStateAsync_NoAliasSet_AddressFallsBackToId()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
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
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
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
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
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
    public async Task LaunchAsync_DelegatesToComponentServiceStart()
    {
        _componentService.StatusAfterStart = new ComponentStatus(ComponentState.Running, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.LaunchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _componentService.StartCallCount);
        Assert.True(state.IsRunning);
        Assert.Equal("123456789", state.Id);
    }

    // ---------------------------------------------------------------- B1: elevation trust

    [Fact]
    public async Task GetStateAsync_ElevatedAndPathNotTrusted_SkipsCliUsesConfigFileOnly()
    {
        _elevationService.IsElevated = true;
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: false);
        _configReader.SetFile(_configReader.SystemConfPath, "ad.anynet.id=111222333\n");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_processRunner.RunArguments);
        Assert.Equal("111222333", state.Id);
    }

    [Theory]
    [InlineData(true, true)] // elevated but the path is trusted: still uses the CLI
    [InlineData(false, false)] // not elevated: uses the CLI even when the path is not trusted
    public async Task GetStateAsync_CliAllowed_UsesCli(bool isElevated, bool pathIsTrusted)
    {
        _elevationService.IsElevated = isElevated;
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: pathIsTrusted);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(_processRunner.RunArguments);
        Assert.Equal("123456789", state.Id);
    }

    // ---------------------------------------------------------------- S4: output validation

    [Fact]
    public async Task GetStateAsync_CliOutputIsBlankLineThenId_TakesFirstNonBlankLine()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetLines("--get-id", 0, ["", "   ", "123456789"]);
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("123456789", state.Id);
    }

    [Fact]
    public async Task GetStateAsync_CliOutputWrappedInAnsiCodes_IsStrippedAndValidated()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 0, "\u001b[32m123456789\u001b[0m");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("123456789", state.Id);
    }

    [Fact]
    public async Task GetStateAsync_CliOutputIsJunk_TreatedAsUnknownFallsBackToConfig()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 0, "not-an-id-at-all");
        _processRunner.SetResult("--get-alias", 0, string.Empty);
        _configReader.SetFile(_configReader.SystemConfPath, "ad.anynet.id=444555666\n");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("444555666", state.Id);
    }

    [Fact]
    public async Task GetStateAsync_ConfigFileIdIsJunk_TreatedAsUnknown()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 1);
        _processRunner.SetResult("--get-alias", 1);
        _configReader.SetFile(_configReader.SystemConfPath, "ad.anynet.id=not-numeric\n");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Null(state.Id);
    }

    [Fact]
    public async Task GetStateAsync_AliasWithoutAtSign_TreatedAsUnknown()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.SetResult("--get-id", 0, "123456789");
        _processRunner.SetResult("--get-alias", 0, "just-a-name-no-at-sign");
        var service = CreateService();

        var state = await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Null(state.Alias);
        Assert.Equal("123456789", state.Address);
    }

    [Fact]
    public async Task GetStateAsync_GetIdTimesOut_SkipsGetAliasCall()
    {
        _componentService.CurrentStatus = new ComponentStatus(ComponentState.Installed, "9.7.16", ExePath, PathIsTrusted: true);
        _processRunner.Hang("--get-id");
        var service = CreateService();

        await service.GetStateAsync(TestContext.Current.CancellationToken);

        Assert.Contains("--get-id", _processRunner.RunArguments);
        Assert.DoesNotContain("--get-alias", _processRunner.RunArguments);
    }
}
