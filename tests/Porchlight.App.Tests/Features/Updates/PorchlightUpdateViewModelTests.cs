using System.Threading.Tasks;
using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.App.Tests.Features.Updates;

public sealed class PorchlightUpdateViewModelTests
{
    private readonly PorchlightUpdateHarness _harness = new();

    [Fact]
    public async Task Check_NoNewerRelease_CardStaysHidden()
    {
        var viewModel = _harness.Create();

        await viewModel.CheckAsync();

        Assert.False(viewModel.IsUpdateAvailable);
        Assert.Equal(string.Empty, viewModel.Headline);
    }

    [Fact]
    public async Task Check_NewerRelease_ShowsHeadlineWithBothVersions()
    {
        _harness.Offer();
        var viewModel = _harness.Create();

        await viewModel.CheckAsync();

        Assert.True(viewModel.IsUpdateAvailable);
        Assert.Equal("Porchlight 0.2.0 is available - you have 0.1.0", viewModel.Headline);
    }

    [Fact]
    public async Task Check_InstalledCopy_OffersUpdateNowNotManualDownload()
    {
        _harness.Offer();
        var viewModel = _harness.Create();

        await viewModel.CheckAsync();

        Assert.True(viewModel.CanUpdateNow);
        Assert.True(viewModel.ShowUpdateNowButton);
        Assert.False(viewModel.ShowManualDownload);
    }

    [Fact]
    public async Task Check_PortableCopy_OffersDownloadLinkAndExplanationOnly()
    {
        _harness.InstallType.Type = InstallType.Portable;
        _harness.Offer();
        var viewModel = _harness.Create();

        await viewModel.CheckAsync();

        Assert.False(viewModel.CanUpdateNow);
        Assert.True(viewModel.ShowManualDownload);
        Assert.Contains("installed version", viewModel.ManualDownloadText);
    }

    [Fact]
    public async Task Check_ReleaseWithoutInstallerAsset_FallsBackToManualDownload()
    {
        _harness.Offer(PorchlightUpdateHarness.Release(withAssets: false));
        var viewModel = _harness.Create();

        await viewModel.CheckAsync();

        Assert.False(viewModel.CanUpdateNow);
        Assert.True(viewModel.ShowManualDownload);
    }

    [Fact]
    public async Task Check_Failure_KeepsThePreviousState()
    {
        _harness.Offer();
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        _harness.Checker.Result = SelfUpdateCheckResult.CouldNotCheck;
        await viewModel.CheckAsync();

        Assert.True(viewModel.IsUpdateAvailable);
    }

    [Fact]
    public async Task Check_LaterUpToDate_HidesTheCard()
    {
        _harness.Offer();
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        _harness.Checker.Result = SelfUpdateCheckResult.UpToDate;
        await viewModel.CheckAsync();

        Assert.False(viewModel.IsUpdateAvailable);
    }

    [Fact]
    public async Task WhatsNew_OpensTheReleasePage()
    {
        _harness.Offer();
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        viewModel.OpenReleaseNotesCommand.Execute(null);

        Assert.Equal(["https://github.com/El1rans/porchlight/releases/tag/v0.2.0"], _harness.UrlLauncher.OpenedUrls);
    }

    [Fact]
    public async Task UpdateNow_DownloadsLaunchesInstallerAndShutsDown()
    {
        _harness.Offer();
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        await viewModel.UpdateNowCommand.ExecuteAsync(null);

        Assert.Equal([_harness.Downloader.Path], _harness.Launcher.Launched);
        Assert.Equal(1, _harness.Lifetime.ShutdownCalls);
        Assert.Null(viewModel.ErrorText);
    }

    [Fact]
    public async Task UpdateNow_DownloadFails_ShowsFriendlyErrorAndDoesNotLaunchOrExit()
    {
        _harness.Offer();
        _harness.Downloader.Failure = new SelfUpdateException("The downloaded update didn't match its checksum.");
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        await viewModel.UpdateNowCommand.ExecuteAsync(null);

        Assert.Equal("The downloaded update didn't match its checksum.", viewModel.ErrorText);
        Assert.Empty(_harness.Launcher.Launched);
        Assert.Equal(0, _harness.Lifetime.ShutdownCalls);
        Assert.False(viewModel.IsWorking);
    }

    [Fact]
    public async Task UpdateNow_UacDeclined_ShowsMessageAndKeepsRunning()
    {
        _harness.Offer();
        _harness.Launcher.Result = InstallerLaunchResult.Declined;
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        await viewModel.UpdateNowCommand.ExecuteAsync(null);

        Assert.Contains("declined", viewModel.ErrorText);
        Assert.Equal(0, _harness.Lifetime.ShutdownCalls);
        Assert.False(viewModel.IsWorking);
        Assert.True(viewModel.ShowUpdateNowButton);
    }

    [Fact]
    public async Task UpdateNow_LaunchFails_ShowsMessageAndKeepsRunning()
    {
        _harness.Offer();
        _harness.Launcher.Result = InstallerLaunchResult.Failed;
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        await viewModel.UpdateNowCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.ErrorText);
        Assert.Equal(0, _harness.Lifetime.ShutdownCalls);
    }

    [Fact]
    public async Task UpdateNow_PortableCopy_DoesNothing()
    {
        _harness.InstallType.Type = InstallType.Portable;
        _harness.Offer();
        var viewModel = _harness.Create();
        await viewModel.CheckAsync();

        await viewModel.UpdateNowCommand.ExecuteAsync(null);

        Assert.Equal(0, _harness.Downloader.Calls);
    }
}
