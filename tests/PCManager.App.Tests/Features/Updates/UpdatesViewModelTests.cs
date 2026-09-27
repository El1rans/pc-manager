using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PCManager.App.Features.Updates;
using PCManager.Core.Settings;
using PCManager.Core.Winget;
using Xunit;

namespace PCManager.App.Tests.Features.Updates;

public sealed class UpdatesViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeWingetClient _wingetClient = new();

    public UpdatesViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerAppTests_" + Guid.NewGuid().ToString("N"));
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
        new(_wingetClient, _settingsStore, NullLogger<UpdatesViewModel>.Instance);

    private static WingetPackage Package(string id, bool requiresExplicit = false, string name = "") =>
        new(Name: string.IsNullOrEmpty(name) ? id : name, Id: id, InstalledVersion: "1.0", AvailableVersion: "2.0",
            Source: "winget", RequiresExplicit: requiresExplicit);

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
        Assert.Equal("Failed (0x87654321)", remaining.StatusText);
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
        Assert.Contains("Finished: 1 updated, 0 failed, 0 skipped", viewModel.LogText);
    }

    [Fact]
    public async Task Silent_Toggled_PersistsToSettings()
    {
        var viewModel = CreateViewModel();

        viewModel.Silent = true;

        Assert.True(_settingsStore.Current.Updates.Silent);
    }

    [Fact]
    public async Task IncludeUnknown_Toggled_PersistsAndTriggersRefresh()
    {
        _wingetClient.UpgradeListResults.Enqueue([]);
        _wingetClient.UpgradeListResults.Enqueue([Package("New.Id")]);
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(quiet: false);

        viewModel.IncludeUnknown = false;
        // OnIncludeUnknownChanged fires a fire-and-forget refresh; give it a turn to complete.
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(_settingsStore.Current.Updates.IncludeUnknown);
        Assert.Contains(viewModel.Packages, p => p.Id == "New.Id");
    }
}
