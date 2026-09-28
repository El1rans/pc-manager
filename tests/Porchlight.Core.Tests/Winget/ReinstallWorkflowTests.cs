using Porchlight.Core.Processes;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class ReinstallWorkflowTests
{
    [Fact]
    public async Task RunAsync_BothStepsSucceed_ReturnsReinstalled()
    {
        var client = new FakeWingetClient();
        var workflow = new ReinstallWorkflow(client);

        var outcome = await workflow.RunAsync("Some.Id", silent: false, log: null, progress: null, CancellationToken.None);

        Assert.Equal(ReinstallOutcomeKind.Reinstalled, outcome.Kind);
        Assert.Equal("Reinstalled", outcome.Title);
        Assert.Equal(WingetSuggestedAction.None, outcome.SuggestedAction);
        Assert.False(outcome.IsCritical);
        Assert.Equal(["uninstall:Some.Id:False", "install:Some.Id:False"], client.Calls);
    }

    [Fact]
    public async Task RunAsync_UninstallFails_StopsBeforeInstallAndReportsUninstallFailed()
    {
        var client = new FakeWingetClient
        {
            UninstallResult = new WingetResult(unchecked((int)0x8A150101), []), // AppInUse
        };
        var workflow = new ReinstallWorkflow(client);

        var outcome = await workflow.RunAsync("Some.Id", silent: false, log: null, progress: null, CancellationToken.None);

        Assert.Equal(ReinstallOutcomeKind.UninstallFailed, outcome.Kind);
        Assert.Null(outcome.InstallOutcome);
        Assert.Equal(WingetSuggestedAction.Retry, outcome.SuggestedAction);
        Assert.False(outcome.IsCritical);
        // The install step must never run - nothing was uninstalled, nothing should be installed.
        Assert.Equal(["uninstall:Some.Id:False"], client.Calls);
        Assert.Contains("still installed", outcome.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_InstallFailsAfterUninstallSucceeds_ReportsCriticalState()
    {
        var client = new FakeWingetClient
        {
            UninstallResult = new WingetResult(0, []),
            InstallResult = new WingetResult(unchecked((int)0x8A150107), []), // InstallNoNetwork
        };
        var workflow = new ReinstallWorkflow(client);

        var outcome = await workflow.RunAsync("Some.Id", silent: false, log: null, progress: null, CancellationToken.None);

        Assert.Equal(ReinstallOutcomeKind.InstallFailedAfterUninstall, outcome.Kind);
        Assert.True(outcome.IsCritical);
        Assert.Equal("Not installed - the new version didn't install", outcome.Title);
        Assert.Equal(WingetSuggestedAction.Retry, outcome.SuggestedAction);
        Assert.Equal(["uninstall:Some.Id:False", "install:Some.Id:False"], client.Calls);
    }

    [Fact]
    public async Task RunAsync_InstallSucceedsWithRestartNeeded_IsStillReinstalled()
    {
        var client = new FakeWingetClient
        {
            UninstallResult = new WingetResult(0, []),
            InstallResult = new WingetResult(unchecked((int)0x8A150109), []), // InstallRebootRequiredToFinish
        };
        var workflow = new ReinstallWorkflow(client);

        var outcome = await workflow.RunAsync("Some.Id", silent: false, log: null, progress: null, CancellationToken.None);

        Assert.Equal(ReinstallOutcomeKind.Reinstalled, outcome.Kind);
        Assert.Equal("Reinstalled - restart needed", outcome.Title);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelled_ThrowsBeforeCallingClientAtAll()
    {
        var client = new FakeWingetClient();
        var workflow = new ReinstallWorkflow(client);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => workflow.RunAsync("Some.Id", silent: false, log: null, progress: null, cts.Token));

        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task RunAsync_PassesSilentFlagToBothSteps()
    {
        var client = new FakeWingetClient();
        var workflow = new ReinstallWorkflow(client);

        await workflow.RunAsync("Some.Id", silent: true, log: null, progress: null, CancellationToken.None);

        Assert.Equal(["uninstall:Some.Id:True", "install:Some.Id:True"], client.Calls);
    }
}
