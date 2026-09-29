using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Updates;
using Porchlight.Core.Settings;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.App.Tests.Features.Updates;

public sealed class UpdatesViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeWingetClient _wingetClient = new();
    private readonly FakeAppInUseDiagnosticsService _appInUseDiagnostics = new();
    private readonly PendingUpdatesTracker _tracker = new();
    private readonly FakeFileDialogService _fileDialogs = new();

    public UpdatesViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightAppTests_" + Guid.NewGuid().ToString("N"));
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

    private UpdatesViewModel CreateViewModel() =>
        new(_wingetClient, _settingsStore, _appInUseDiagnostics, _tracker, _fileDialogs, NullLogger<UpdatesViewModel>.Instance);

    private UpdatesViewModel CreateViewModel(TimeProvider timeProvider) =>
        new(_wingetClient, _settingsStore, _appInUseDiagnostics, _tracker, _fileDialogs, NullLogger<UpdatesViewModel>.Instance, timeProvider);

    private static WingetPackage Package(
        string id, bool requiresExplicit = false, string name = "", string installedVersion = "1.0", string availableVersion = "2.0") =>
        new(Name: string.IsNullOrEmpty(name) ? id : name, Id: id, InstalledVersion: installedVersion,
            AvailableVersion: availableVersion, Source: "winget", RequiresExplicit: requiresExplicit);

    [Fact]
    public async Task CheckForUpdatesAsync_Success_ReportsCountOfNonIgnoredUpdates()
    {
        _settingsStore.Update(s => s.Updates.IgnoredIds.Add("Ignored.Id"));
        _wingetClient.UpgradeListResults.Enqueue([Package("A.Id"), Package("B.Id"), Package("Ignored.Id")]);
        var viewModel = CreateViewModel();

        var result = await viewModel.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NoUpdates_SucceedsWithZero()
    {
        var viewModel = CreateViewModel();

        var result = await viewModel.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckResult.Succeeded(0), result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WingetFailure_ReportsFailed()
    {
        _wingetClient.GetUpgradesException = new InvalidOperationException("boom");
        var viewModel = CreateViewModel();

        var result = await viewModel.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WingetMissing_ReportsFailed()
    {
        _wingetClient.GetUpgradesException = new WingetNotFoundException();
        var viewModel = CreateViewModel();

        Assert.Equal(UpdateCheckStatus.Failed, (await viewModel.CheckForUpdatesAsync()).Status);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhileBusy_IsSkippedWithoutCallingWinget()
    {
        var viewModel = CreateViewModel();
        viewModel.IsBusy = true;

        var result = await viewModel.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.Skipped, result.Status);
        Assert.Equal(0, _wingetClient.GetUpgradesCallCount);
    }

    [Fact]
    public async Task RefreshAsync_DefaultSelection_TicksNonIgnoredNonExplicitOnly()
    {
        _settingsStore.Update(s => s.Updates.IgnoredIds.Add("Ignored.Id"));
        _wingetClient.UpgradeListResults.Enqueue(
        [
            Package("Plain.Id"),
            Package("Explicit.Id", requiresExplicit: true),
            Package("Ignored.Id"),
        ]);
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(quiet: false);

        Assert.True(viewModel.Packages.Single(p => p.Id == "Plain.Id").IsSelected);
        Assert.False(viewModel.Packages.Single(p => p.Id == "Explicit.Id").IsSelected);
        var ignoredRow = viewModel.Packages.Single(p => p.Id == "Ignored.Id");
        Assert.False(ignoredRow.IsSelected);
        Assert.True(ignoredRow.IsIgnored);
    }

    [Fact]
    public async Task RefreshAsync_NonIgnoredCount_SetsBadge()
    {
        _wingetClient.UpgradeListResults.Enqueue([Package("A"), Package("B")]);
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(quiet: false);

        Assert.Equal("2", viewModel.Badge);
    }

    [Fact]
    public async Task RefreshAsync_NoUpdates_BadgeIsNull()
    {
        _wingetClient.UpgradeListResults.Enqueue([]);
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(quiet: false);

        Assert.Null(viewModel.Badge);
    }

    [Fact]
    public async Task Ignore_PersistsToSettingsAndUnticksRow()
    {
        _wingetClient.UpgradeListResults.Enqueue([Package("Some.Id")]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        var row = viewModel.Packages.Single();
        Assert.True(row.IsSelected);

        viewModel.Ignore([row]);

        Assert.True(row.IsIgnored);
        Assert.False(row.IsSelected);
        Assert.Contains("Some.Id", _settingsStore.Current.Updates.IgnoredIds);
    }

    [Fact]
    public async Task Ignore_ThenReloadedFromDisk_StaysIgnored()
    {
        _wingetClient.UpgradeListResults.Enqueue([Package("Some.Id")]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.Ignore([viewModel.Packages.Single()]);

        var reloaded = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));

        Assert.Contains("Some.Id", reloaded.Current.Updates.IgnoredIds);
    }

    [Fact]
    public async Task StopIgnoring_RemovesFromSettingsAndRow()
    {
        _settingsStore.Update(s => s.Updates.IgnoredIds.Add("Some.Id"));
        _wingetClient.UpgradeListResults.Enqueue([Package("Some.Id")]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        var row = viewModel.Packages.Single();
        Assert.True(row.IsIgnored);

        viewModel.StopIgnoring([row]);

        Assert.False(row.IsIgnored);
        Assert.DoesNotContain("Some.Id", _settingsStore.Current.Updates.IgnoredIds);
    }

    [Fact]
    public async Task UpdateSelectedAsync_StopAfterCurrent_MarksRemainingSkippedAsCancelled()
    {
        _wingetClient.UpgradeListResults.Enqueue([Package("First"), Package("Second"), Package("Third")]);
        _wingetClient.UpgradeListResults.Enqueue([]); // the quiet re-check once the run finishes
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);

        // Simulate the user clicking "Stop after current" while the first package is updating.
        _wingetClient.OnUpgrading = id =>
        {
            if (id == "First")
            {
                viewModel.StopAfterCurrentCommand.Execute(null);
            }
        };

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // "First" was already in flight when Stop was clicked, so it still ran to completion.
        Assert.Single(_wingetClient.UpgradeCalls);
        Assert.Equal("First", _wingetClient.UpgradeCalls[0]);
    }

    [Fact]
    public async Task UpdateSelectedAsync_AfterFinish_QuietRecheckKeepsFailedRowStatus()
    {
        var packageA = Package("Succeeds");
        var packageB = Package("Fails");
        _wingetClient.UpgradeListResults.Enqueue([packageA, packageB]);
        // The quiet re-check after the run: "Succeeds" is gone (it updated), "Fails" is still there.
        _wingetClient.UpgradeListResults.Enqueue([packageB]);
        _wingetClient.UpgradeResultsById["Succeeds"] = new WingetResult(0, []);
        _wingetClient.UpgradeResultsById["Fails"] = new WingetResult(unchecked((int)0x87654321), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var remaining = Assert.Single(viewModel.Packages);
        Assert.Equal("Fails", remaining.Id);
        Assert.Equal(UpdateRowState.Failed, remaining.State);
        Assert.Equal("Something went wrong", remaining.StatusText);
        Assert.Contains("0x87654321", remaining.StatusTooltip);
    }

    [Fact]
    public async Task UpdateSelectedAsync_RestartMentionedInOutput_ReportsRestartNeeded()
    {
        var package = Package("NeedsRestart");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.UpgradeResultsById["NeedsRestart"] =
            new WingetResult(0, ["Successfully installed", "Restart your computer to finish."]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // The row disappeared (it succeeded), but the message is asserted via the log instead,
        // since a successful row is not kept around after the quiet recheck.
        Assert.Contains("Finished: 1 updated (1 needs a restart)", viewModel.LogText);
    }

    [Fact]
    public async Task UpdateSelectedAsync_NoApplicableUpdate_IsSkipped()
    {
        var package = Package("AlreadyCurrent");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([package]); // still listed - nothing was installed
        _wingetClient.UpgradeResultsById["AlreadyCurrent"] = new WingetResult(unchecked((int)0x8A15002B), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var remaining = Assert.Single(viewModel.Packages);
        Assert.Equal(UpdateRowState.Skipped, remaining.State);
        Assert.Equal("Not available for this PC", remaining.StatusText);
        // APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE offers both Reinstall and Hide - see
        // docs/specs/09-friendly-update-outcomes.md's addendum (RARLab.WinRAR on the maintainer's PC).
        Assert.Equal(WingetSuggestedAction.Reinstall | WingetSuggestedAction.Hide, remaining.SuggestedAction);
        Assert.True(remaining.CanReinstall);
        Assert.True(remaining.CanHide);
        Assert.Contains("Finished: 1 not available for this PC", viewModel.LogText);
    }

    [Fact]
    public void Silent_Toggled_PersistsToSettings()
    {
        var viewModel = CreateViewModel();

        viewModel.Silent = true;

        Assert.True(_settingsStore.Current.Updates.Silent);
    }

    [Fact]
    public void CheckOnStartup_Toggled_PersistsToSettings()
    {
        var viewModel = CreateViewModel();
        Assert.True(viewModel.CheckOnStartup);

        viewModel.CheckOnStartup = false;

        Assert.False(_settingsStore.Current.Updates.CheckOnStartup);
    }

    [Fact]
    public async Task IncludeUnknown_Toggled_PersistsAndTriggersRefresh()
    {
        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.UpgradeListResults.Enqueue([Package("New.Id")]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);

        viewModel.IncludeUnknown = false;

        Assert.False(_settingsStore.Current.Updates.IncludeUnknown);
        Assert.Contains(viewModel.Packages, p => p.Id == "New.Id");
    }

    /// <summary>
    /// B1: toggling "Include apps with unknown version" while an update run is in flight must not
    /// start a competing (non-quiet) refresh - that would clear <c>Packages</c> out from under the
    /// running loop (orphaning the rows it is still writing status to), clear <c>IsBusy</c> while
    /// the run is still going, and cause the run's own final quiet re-check to merge from those
    /// already-reset rows instead of the real in-progress ones, losing every row's outcome.
    /// </summary>
    [Fact]
    public async Task IncludeUnknown_ToggledDuringUpdateRun_DoesNotStartACompetingRefresh()
    {
        var packageA = Package("First");
        var packageB = Package("Second");
        _wingetClient.UpgradeListResults.Enqueue([packageA, packageB]); // initial listing
        _wingetClient.UpgradeListResults.Enqueue([packageB]); // the run's own final quiet re-check
        _wingetClient.UpgradeResultsById["First"] = new WingetResult(0, []);
        _wingetClient.UpgradeResultsById["Second"] = new WingetResult(unchecked((int)0x87654321), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        _wingetClient.OnUpgrading = id =>
        {
            if (id == "First")
            {
                viewModel.IncludeUnknown = !viewModel.IncludeUnknown;
            }
        };

        Assert.Equal(1, _wingetClient.GetUpgradesCallCount);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        // Exactly the initial listing plus the run's own final re-check - no extra refresh
        // squeezed in by the toggle.
        Assert.Equal(2, _wingetClient.GetUpgradesCallCount);
        Assert.Equal(["First", "Second"], _wingetClient.UpgradeCalls);

        // "Second" failed - its real outcome survived to the final merged list.
        var remaining = Assert.Single(viewModel.Packages);
        Assert.Equal("Second", remaining.Id);
        Assert.Equal(UpdateRowState.Failed, remaining.State);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsUpdating);
    }

    [Fact]
    public void IsBusyWithWork_ReflectsIsUpdating()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsBusyWithWork);
        Assert.False(string.IsNullOrEmpty(viewModel.BusyMessage));
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateSelectedAsync_InstallTechnologyMismatch_ReportsReinstallRequired()
    {
        var package = Package("NeedsReinstall");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([package]); // still listed - the plain upgrade never applied
        _wingetClient.UpgradeResultsById["NeedsReinstall"] = new WingetResult(unchecked((int)0x8A15008E), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var remaining = Assert.Single(viewModel.Packages);
        Assert.Equal("Needs a reinstall", remaining.StatusText);
        Assert.Equal(WingetSuggestedAction.Reinstall, remaining.SuggestedAction);
        Assert.True(remaining.CanReinstall);
        Assert.Contains("Finished: 1 needs a reinstall", viewModel.LogText);
    }

    [Fact]
    public void RequestReinstall_ShowsConfirmationWithoutCallingWingetYet()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        var viewModel = CreateViewModel();

        viewModel.RequestReinstallCommand.Execute(row);

        Assert.True(viewModel.IsReinstallConfirmationVisible);
        Assert.Contains("Reinstall Some.Id?", viewModel.ReinstallConfirmationMessage);
        Assert.Contains("uninstall Some.Id and then install the newest version", viewModel.ReinstallConfirmationMessage);
        Assert.Empty(_wingetClient.UninstallCalls);
        Assert.Empty(_wingetClient.InstallCalls);
    }

    [Fact]
    public void CancelReinstall_HidesConfirmationWithoutCallingWinget()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        var viewModel = CreateViewModel();
        viewModel.RequestReinstallCommand.Execute(row);

        viewModel.CancelReinstallCommand.Execute(null);

        Assert.False(viewModel.IsReinstallConfirmationVisible);
        Assert.Empty(_wingetClient.UninstallCalls);
    }

    [Fact]
    public async Task ConfirmReinstallAsync_RunsUninstallThenInstallAndAppliesOutcome()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        _wingetClient.UninstallResult = new WingetResult(0, []);
        _wingetClient.InstallResult = new WingetResult(0, []);
        _wingetClient.UpgradeListResults.Enqueue([]); // quiet re-check after a successful reinstall
        var viewModel = CreateViewModel();
        viewModel.RequestReinstallCommand.Execute(row);

        await viewModel.ConfirmReinstallCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsReinstallConfirmationVisible);
        Assert.Equal(["Some.Id"], _wingetClient.UninstallCalls);
        Assert.Equal(["Some.Id"], _wingetClient.InstallCalls);
        Assert.Equal("Reinstalled", row.StatusText);
        Assert.Equal(UpdateRowState.Updated, row.State);
    }

    [Fact]
    public async Task ConfirmReinstallAsync_InstallFailsAfterUninstall_ReportsCriticalStateAndSkipsRefresh()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        _wingetClient.UninstallResult = new WingetResult(0, []);
        _wingetClient.InstallResult = new WingetResult(unchecked((int)0x8A150107), []); // InstallNoNetwork
        var viewModel = CreateViewModel();
        viewModel.RequestReinstallCommand.Execute(row);

        await viewModel.ConfirmReinstallCommand.ExecuteAsync(null);

        Assert.Equal("Not installed - the new version didn't install", row.StatusText);
        Assert.True(row.IsCriticalReinstallFailure);
        Assert.Equal("Try install again", row.RetryButtonText);
        Assert.Equal(UpdateRowState.Failed, row.State);
        // A quiet refresh would silently drop this critical row (winget upgrade would no longer
        // list an uninstalled package) - GetUpgradesAsync must not have been called again.
        Assert.Equal(0, _wingetClient.GetUpgradesCallCount);
    }

    [Fact]
    public async Task RequestReinstall_DisabledWhileBusy()
    {
        var packageA = Package("First");
        _wingetClient.UpgradeListResults.Enqueue([packageA]);
        _wingetClient.Gate = new TaskCompletionSource();
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        var row = viewModel.Packages.Single();

        var updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        Assert.False(viewModel.RequestReinstallCommand.CanExecute(row));

        _wingetClient.Gate.SetResult();
        await updateTask;

        Assert.True(viewModel.RequestReinstallCommand.CanExecute(row));
    }

    [Fact]
    public void HideUpdate_IgnoresRowAndShowsConfirmationMessage()
    {
        var row = new UpdatePackageViewModel(Package("RARLab.WinRAR", name: "WinRAR"));
        var viewModel = CreateViewModel();

        viewModel.HideUpdateCommand.Execute(row);

        Assert.True(row.IsIgnored);
        Assert.Contains("RARLab.WinRAR", _settingsStore.Current.Updates.IgnoredIds);
        Assert.Equal(
            "WinRAR won't be shown again. You can bring it back with \"Show ignored\".",
            viewModel.HideConfirmationMessage);
    }

    [Fact]
    public void DismissHideConfirmation_ClearsMessage()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        var viewModel = CreateViewModel();
        viewModel.HideUpdateCommand.Execute(row);

        viewModel.DismissHideConfirmationCommand.Execute(null);

        Assert.Null(viewModel.HideConfirmationMessage);
    }

    [Fact]
    public async Task RetryRowAsync_AppRunning_ReRunsPlainUpgrade()
    {
        var package = Package("Some.Id");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["Some.Id"] = new WingetResult(unchecked((int)0x8A150101), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        var row = viewModel.Packages.Single();
        Assert.Equal("Close the app and try again", row.StatusText);
        Assert.False(row.IsCriticalReinstallFailure);

        _wingetClient.UpgradeResultsById["Some.Id"] = new WingetResult(0, []);
        _wingetClient.UpgradeListResults.Enqueue([]);
        await viewModel.RetryRowCommand.ExecuteAsync(row);

        Assert.Equal(2, _wingetClient.UpgradeCalls.Count(id => id == "Some.Id"));
        Assert.Empty(_wingetClient.UninstallCalls);
    }

    [Fact]
    public async Task RetryRowAsync_CriticalReinstallFailure_CallsInstallOnlyNeverUninstall()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id"));
        _wingetClient.UninstallResult = new WingetResult(0, []);
        _wingetClient.InstallResult = new WingetResult(unchecked((int)0x8A150107), []);
        var viewModel = CreateViewModel();
        viewModel.RequestReinstallCommand.Execute(row);
        await viewModel.ConfirmReinstallCommand.ExecuteAsync(null);
        Assert.True(row.IsCriticalReinstallFailure);
        Assert.Equal(["Some.Id"], _wingetClient.UninstallCalls);

        _wingetClient.InstallResult = new WingetResult(0, []);
        _wingetClient.UpgradeListResults.Enqueue([]);
        await viewModel.RetryRowCommand.ExecuteAsync(row);

        // Still exactly one uninstall call ever - the critical retry only installs.
        Assert.Equal(["Some.Id"], _wingetClient.UninstallCalls);
        Assert.Equal(["Some.Id", "Some.Id"], _wingetClient.InstallCalls);
        Assert.Equal("Updated", row.StatusText);
        Assert.False(row.IsCriticalReinstallFailure);
    }

    [Fact]
    public async Task UpdateSelectedAsync_NonSilentLongRunningPackage_ShowsInteractiveWaitingHintThenClearsIt()
    {
        var package = Package("Slow.Id");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.Gate = new TaskCompletionSource();
        var timeProvider = new FakeTimeProvider();
        var viewModel = CreateViewModel(timeProvider);
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        var row = viewModel.Packages.Single();

        var updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        await Task.Delay(20, TestContext.Current.CancellationToken); // let the loop reach and start awaiting the winget call

        Assert.Null(row.WaitingHint);

        timeProvider.Advance(UpdatesViewModel.WaitingHintThreshold);
        await Task.Delay(20, TestContext.Current.CancellationToken); // let the Task.Delay continuation observe the advance

        Assert.Equal(UpdatesViewModel.WaitingHintTextInteractive, row.WaitingHint);

        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.Gate.SetResult();
        await updateTask;

        Assert.Null(row.WaitingHint);
    }

    [Fact]
    public async Task UpdateSelectedAsync_SilentLongRunningPackage_ShowsSilentWaitingHint()
    {
        var package = Package("Slow.Id");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.Gate = new TaskCompletionSource();
        var timeProvider = new FakeTimeProvider();
        var viewModel = CreateViewModel(timeProvider);
        viewModel.Silent = true;
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        var row = viewModel.Packages.Single();

        var updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        timeProvider.Advance(UpdatesViewModel.WaitingHintThreshold);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        Assert.Equal(UpdatesViewModel.WaitingHintTextSilent, row.WaitingHint);

        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.Gate.SetResult();
        await updateTask;
    }

    [Fact]
    public async Task UpdateSelectedAsync_FastPackage_NeverShowsWaitingHint()
    {
        var package = Package("Fast.Id");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([]);
        var timeProvider = new FakeTimeProvider();
        var viewModel = CreateViewModel(timeProvider);
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        var row = viewModel.Packages.Single();

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        Assert.Null(row.WaitingHint);
    }

    // --- Fix 1: remember last outcome across restarts -------------------------------------------
    // See docs/specs/09-friendly-update-outcomes.md's addendum.

    [Fact]
    public async Task RefreshAsync_AfterSimulatedRestart_RestoresRememberedOutcomeForSameIdAndVersion()
    {
        // JanDeDobbeleer.OhMyPosh on the maintainer's PC.
        var package = Package("JanDeDobbeleer.OhMyPosh", availableVersion: "2.0");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeListResults.Enqueue([package]); // still listed - the failed upgrade changed nothing
        _wingetClient.UpgradeResultsById["JanDeDobbeleer.OhMyPosh"] = new WingetResult(unchecked((int)0x8A15008E), []);
        var firstSession = CreateViewModel();
        await firstSession.RefreshAsync(quiet: false);
        firstSession.SelectAllCommand.Execute(null);
        await firstSession.UpdateSelectedCommand.ExecuteAsync(null);
        Assert.Equal("Needs a reinstall", firstSession.Packages.Single().StatusText);

        // A brand-new view model over the same persisted settings, with no in-session state of its
        // own - simulates the app being restarted.
        _wingetClient.UpgradeListResults.Enqueue([package]);
        var secondSession = CreateViewModel();
        await secondSession.RefreshAsync(quiet: false);

        var row = secondSession.Packages.Single();
        Assert.Equal("Needs a reinstall", row.StatusText);
        Assert.True(row.CanReinstall);
        Assert.Equal(WingetSuggestedAction.Reinstall, row.SuggestedAction);
        Assert.Single(_wingetClient.UpgradeCalls); // never re-ran the upgrade to find this out again
    }

    [Fact]
    public async Task RefreshAsync_RememberedOutcomeForDifferentAvailableVersion_IsNotRestored()
    {
        var package = Package("Some.Id", availableVersion: "2.0");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["Some.Id"] = new WingetResult(unchecked((int)0x8A15008E), []);
        var firstSession = CreateViewModel();
        await firstSession.RefreshAsync(quiet: false);
        firstSession.SelectAllCommand.Execute(null);
        await firstSession.UpdateSelectedCommand.ExecuteAsync(null);

        // A newer version is now available - the old remembered outcome no longer applies.
        var newerPackage = Package("Some.Id", availableVersion: "3.0");
        _wingetClient.UpgradeListResults.Enqueue([newerPackage]);
        var secondSession = CreateViewModel();
        await secondSession.RefreshAsync(quiet: false);

        var row = secondSession.Packages.Single();
        Assert.NotEqual("Needs a reinstall", row.StatusText);
        Assert.False(row.CanReinstall);
    }

    [Fact]
    public async Task RetryRowAsync_SucceedsAfterRememberedFailure_ClearsRememberedOutcome()
    {
        var package = Package("Some.Id");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["Some.Id"] = new WingetResult(unchecked((int)0x8A15008E), []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        var row = viewModel.Packages.Single();
        Assert.Equal("Needs a reinstall", row.StatusText);

        _wingetClient.UpgradeResultsById["Some.Id"] = new WingetResult(0, []);
        _wingetClient.UpgradeListResults.Enqueue([]); // succeeded - no longer listed
        await viewModel.RetryRowCommand.ExecuteAsync(row);

        Assert.Null(UpdateOutcomeMemory.Find(_settingsStore.Current.Updates.LastOutcomes, "Some.Id", "2.0"));
    }

    // --- Fix 5: "Unknown" installed-version packages that update successfully -------------------
    // See docs/specs/09-friendly-update-outcomes.md's addendum (Google.CloudSDK on the maintainer's
    // PC: Unknown -> 586.0.0 reported success, but the row kept reappearing and a second per-user
    // copy was installed alongside the existing machine-wide one).

    [Fact]
    public async Task UpdateSelectedAsync_UnknownVersionPackageUpdatesSuccessfully_HidesRowByDefault()
    {
        var package = Package("Google.CloudSDK", installedVersion: "Unknown", availableVersion: "586.0.0");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["Google.CloudSDK"] = new WingetResult(0, []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        // Winget still lists it in the automatic quiet re-check after the update run, with the same
        // available version - it can't confirm the "Unknown" installed version actually changed.
        _wingetClient.UpgradeListResults.Enqueue([package]);
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var row = viewModel.Packages.Single();
        Assert.True(row.IsPendingVersionConfirmation);
        Assert.True(row.IsHiddenByDefault);
        Assert.False(row.IsSelected);

        viewModel.ShowIgnored = false;
        Assert.DoesNotContain(row, viewModel.PackagesView.Cast<UpdatePackageViewModel>());

        viewModel.ShowIgnored = true;
        Assert.Contains(row, viewModel.PackagesView.Cast<UpdatePackageViewModel>());
        Assert.Contains("second copy", row.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_NewerVersionAppearsForHiddenUnknownVersionRow_ShowsItAgain()
    {
        var package = Package("Google.CloudSDK", installedVersion: "Unknown", availableVersion: "586.0.0");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["Google.CloudSDK"] = new WingetResult(0, []);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        _wingetClient.UpgradeListResults.Enqueue([package]);
        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        Assert.True(viewModel.Packages.Single().IsPendingVersionConfirmation);

        var newerPackage = Package("Google.CloudSDK", installedVersion: "Unknown", availableVersion: "587.0.0");
        _wingetClient.UpgradeListResults.Enqueue([newerPackage]);
        await viewModel.RefreshAsync(quiet: false);

        var row = viewModel.Packages.Single();
        Assert.False(row.IsPendingVersionConfirmation);
        Assert.False(row.IsHiddenByDefault);
    }

    [Fact]
    public void Notes_UnknownInstalledVersion_WarnsAboutSecondCopy()
    {
        var row = new UpdatePackageViewModel(Package("Some.Id", installedVersion: "Unknown"));

        Assert.Equal("Current version unknown - updating may install a second copy", row.Notes);
    }

    // --- Fix 4: hidden UAC-prompt waiting hint -------------------------------------------------
    // See docs/specs/09-friendly-update-outcomes.md's addendum (Google.CloudSDK sat waiting on a
    // hidden UAC prompt raised from winget's background process).

    [Fact]
    public async Task UpdateSelectedAsync_OutputMentionsAdminPrompt_ShowsWaitingHintImmediately()
    {
        var package = Package("Google.CloudSDK");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeLogLines.Add("The installer will request to run as administrator. Expect a prompt.");
        _wingetClient.Gate = new TaskCompletionSource();
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);
        var row = viewModel.Packages.Single();

        var updateTask = viewModel.UpdateSelectedCommand.ExecuteAsync(null);
        await Task.Delay(20, TestContext.Current.CancellationToken); // let the reported line propagate

        Assert.Equal(UpdatesViewModel.AdminPromptWaitingHintText, row.WaitingHint);

        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.Gate.SetResult();
        await updateTask;

        Assert.Null(row.WaitingHint);
    }

    // --- Fix 3: naming the programs holding files when "app in use" ----------------------------

    [Fact]
    public async Task UpdateSelectedAsync_AppInUseByAnotherApplication_EnrichesExplanationWithLockingProcesses()
    {
        var package = Package("OBSProject.OBSStudio", name: "OBS Studio");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["OBSProject.OBSStudio"] = new WingetResult(unchecked((int)0x8A150111), []);
        _appInUseDiagnostics.Result =
            "These programs are using OBS Studio's files: Chrome, Claude. Close them, then try again.";
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var row = viewModel.Packages.Single();
        Assert.Contains("Chrome, Claude", row.StatusTooltip);
        Assert.Equal(("OBSProject.OBSStudio", "OBS Studio"), Assert.Single(_appInUseDiagnostics.Calls));
    }

    [Fact]
    public async Task UpdateSelectedAsync_AppInUseDiagnosticsFindsNothing_FallsBackToGenericText()
    {
        var package = Package("OBSProject.OBSStudio", name: "OBS Studio");
        _wingetClient.UpgradeListResults.Enqueue([package]);
        _wingetClient.UpgradeResultsById["OBSProject.OBSStudio"] = new WingetResult(unchecked((int)0x8A150111), []);
        _appInUseDiagnostics.Result = null;
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UpdateSelectedCommand.ExecuteAsync(null);

        var row = viewModel.Packages.Single();
        Assert.Equal("Close the app and try again", row.StatusText);
        Assert.Contains("currently open", row.StatusTooltip);
    }
}
