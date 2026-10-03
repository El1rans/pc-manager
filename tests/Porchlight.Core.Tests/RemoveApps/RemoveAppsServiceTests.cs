using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Porchlight.Core.RemoveApps;
using Porchlight.Core.Tests.Winget;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.RemoveApps;

public sealed class RemoveAppsServiceTests
{
    private readonly FakeReader _reader = new();
    private readonly FakeWingetClient _winget = new();
    private readonly FakeUninstaller _uninstaller = new();

    private RemoveAppsService Create() => new(_reader, _winget, _uninstaller, NullLogger<RemoveAppsService>.Instance);

    private static InstalledApp App(string name, string? version = "1.0", bool perMachine = true) =>
        new(name, "Pub", version, null, null, @"""C:\x\unins000.exe""", perMachine);

    private async Task<(RemoveAppsService Service, IReadOnlyList<RemovableApp> List)> ListAsync(params InstalledApp[] apps)
    {
        _reader.Apps = apps;
        var service = Create();
        return (service, await service.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task List_ClassifiesHidesPorchlightAndMapsWingetIds()
    {
        _winget.InstalledPackages.Add(new WingetInstalledPackage("VLC media player", "VideoLAN.VLC", "3.0"));

        var (_, list) = await ListAsync(
            App("VLC media player"), App("Porchlight"), App("AnyDesk"), App("Microsoft .NET Runtime - 8.0"), App("McAfee LiveSafe"));

        Assert.DoesNotContain(list, a => a.App.DisplayName == "Porchlight");
        Assert.Equal("VideoLAN.VLC", list.Single(a => a.App.DisplayName == "VLC media player").WingetId);
        Assert.Equal(RemovableAppKind.ManagedByPorchlight, list.Single(a => a.App.DisplayName == "AnyDesk").Kind);
        Assert.Equal(RemovableAppKind.SystemPart, list.Single(a => a.App.DisplayName.StartsWith("Microsoft .NET", StringComparison.Ordinal)).Kind);
        Assert.True(list.Single(a => a.App.DisplayName == "McAfee LiveSafe").IsOftenPreinstalled);
        Assert.False(list.Single(a => a.App.DisplayName == "VLC media player").IsOftenPreinstalled);
    }

    [Fact]
    public async Task List_WingetMissing_StillListsAppsWithoutIds()
    {
        _winget.ListException = new WingetNotFoundException();

        var (_, list) = await ListAsync(App("VLC media player"));

        Assert.Null(Assert.Single(list).WingetId);
    }

    [Fact]
    public async Task Remove_MappedApp_UsesWingetExactAndRaisesTheEvent()
    {
        _winget.InstalledPackages.Add(new WingetInstalledPackage("VLC media player", "VideoLAN.VLC", "1.0"));
        var (service, list) = await ListAsync(App("VLC media player"));
        var raised = new List<RemovableApp>();
        service.AppRemoved += (_, e) => raised.Add(e.App);

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.Removed, outcome.Result);
        Assert.Equal(["uninstall:VideoLAN.VLC:True"], _winget.Calls);
        Assert.Empty(_uninstaller.Started);
        Assert.Equal(list[0], Assert.Single(raised));
    }

    [Fact]
    public async Task Remove_UnmappedApp_OpensTheOwnUninstallerInteractively()
    {
        var (service, list) = await ListAsync(App("Obscure Tool"));
        var raised = 0;
        service.AppRemoved += (_, _) => raised++;

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.UninstallerOpened, outcome.Result);
        Assert.Empty(_winget.Calls);
        Assert.Equal("Obscure Tool", Assert.Single(_uninstaller.Started).DisplayName);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Remove_WingetMissing_FallsBackToTheOwnUninstaller()
    {
        _winget.InstalledPackages.Add(new WingetInstalledPackage("VLC media player", "VideoLAN.VLC", "1.0"));
        var (service, list) = await ListAsync(App("VLC media player"));
        _winget.UninstallException = new WingetNotFoundException();

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.UninstallerOpened, outcome.Result);
        Assert.Single(_uninstaller.Started);
    }

    [Fact]
    public async Task Remove_WingetFails_ReportsThePlainOutcomeAndRaisesNothing()
    {
        _winget.InstalledPackages.Add(new WingetInstalledPackage("VLC media player", "VideoLAN.VLC", "1.0"));
        var (service, list) = await ListAsync(App("VLC media player"));
        _winget.UninstallResult = new WingetResult(unchecked((int)0x8A150011), []);
        var raised = 0;
        service.AppRemoved += (_, _) => raised++;

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.WingetProblem, outcome.Result);
        Assert.NotNull(outcome.WingetOutcome);
        Assert.Empty(_uninstaller.Started);
        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData("AnyDesk")]
    [InlineData("OpenRGB")]
    [InlineData("PawnIO")]
    public async Task Remove_ManagedComponent_IsRefused(string name)
    {
        var (service, list) = await ListAsync(App(name));

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.Refused, outcome.Result);
        Assert.Empty(_winget.Calls);
        Assert.Empty(_uninstaller.Started);
    }

    [Fact]
    public async Task Remove_PorchlightItself_IsRefusedEvenIfConstructedByHand()
    {
        var (service, _) = await ListAsync(App("Something"));
        var porchlight = new RemovableApp(App("Porchlight"), RemovableAppKind.Porchlight, false, null);

        var outcome = await service.RemoveAsync(porchlight, TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.Refused, outcome.Result);
        Assert.Empty(_uninstaller.Started);
    }

    [Fact]
    public async Task Remove_AppNotInTheLastList_IsRefused()
    {
        var (service, _) = await ListAsync(App("Listed"));
        var stranger = new RemovableApp(App("Stranger"), RemovableAppKind.Normal, false, "Evil.Id");

        var outcome = await service.RemoveAsync(stranger, TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.Refused, outcome.Result);
        Assert.Empty(_winget.Calls);
    }

    [Fact]
    public async Task Remove_SystemPart_IsAllowed()
    {
        var (service, list) = await ListAsync(App("Microsoft Visual C++ 2012 Redistributable (x86)"));

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.UninstallerOpened, outcome.Result);
    }

    [Fact]
    public async Task Remove_BlockedWhileElevated_StartsNothing()
    {
        _winget.InstalledPackages.Add(new WingetInstalledPackage("VLC media player", "VideoLAN.VLC", "1.0"));
        var (service, list) = await ListAsync(App("VLC media player", perMachine: false));
        _uninstaller.CanStart = false;

        var outcome = await service.RemoveAsync(list[0], TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppResult.BlockedWhileElevated, outcome.Result);
        Assert.Empty(_winget.Calls);
        Assert.Empty(_uninstaller.Started);
    }

    [Fact]
    public async Task Remove_UninstallerFailsOrIsUnparseable_IsReported()
    {
        var (service, list) = await ListAsync(App("Obscure Tool"));

        _uninstaller.Result = UninstallStartResult.Failed;
        Assert.Equal(RemoveAppResult.Failed, (await service.RemoveAsync(list[0], TestContext.Current.CancellationToken)).Result);

        _uninstaller.Result = UninstallStartResult.InvalidCommand;
        Assert.Equal(RemoveAppResult.InvalidCommand, (await service.RemoveAsync(list[0], TestContext.Current.CancellationToken)).Result);
    }

    private sealed class FakeReader : IInstalledAppsReader
    {
        public IReadOnlyList<InstalledApp> Apps { get; set; } = [];

        public IReadOnlyList<InstalledApp> GetInstalledApps() => Apps;

        public IReadOnlyList<InstalledApp> GetAllInstalledApps() => Apps;
    }

    private sealed class FakeUninstaller : IAppUninstaller
    {
        public bool CanStart { get; set; } = true;

        public UninstallStartResult Result { get; set; } = UninstallStartResult.Started;

        public List<InstalledApp> Started { get; } = [];

        public bool CanStartUninstall(InstalledApp app) => CanStart;

        public UninstallStartResult StartUninstall(InstalledApp app)
        {
            if (Result == UninstallStartResult.Started)
            {
                Started.Add(app);
            }

            return Result;
        }
    }
}
