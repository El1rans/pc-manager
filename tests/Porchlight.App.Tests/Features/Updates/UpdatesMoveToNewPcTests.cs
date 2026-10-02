using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Updates;
using Porchlight.Core.Settings;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.App.Tests.Features.Updates;

public sealed class UpdatesMoveToNewPcTests : IDisposable
{
    private const string ValidExport = "{\"Sources\":[{\"Packages\":[{\"PackageIdentifier\":\"VideoLAN.VLC\"},{\"PackageIdentifier\":\"7zip.7zip\"}]}]}";

    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeWingetClient _wingetClient = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly PendingUpdatesTracker _tracker = new();

    public UpdatesMoveToNewPcTests()
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

    private UpdatesViewModel Create() =>
        new(_wingetClient, _settingsStore, new FakeAppInUseDiagnosticsService(), _tracker, _dialogs,
            new PorchlightUpdateHarness().Create(), new FakeUpdateHistoryStore(), new FakeHistoryConfirmation(),
            NullLogger<UpdatesViewModel>.Instance);

    private string WriteFile(string content)
    {
        var path = Path.Combine(_directory, "apps.json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task SaveAppList_RunsExportWithChosenPath()
    {
        var path = Path.Combine(_directory, "export.json");
        _dialogs.SavePath = path;
        _wingetClient.ExportResult = new WingetResult(0, []);
        File.WriteAllText(path, "{}");
        var viewModel = Create();

        await viewModel.SaveAppListCommand.ExecuteAsync(null);

        Assert.Equal([path], _wingetClient.ExportCalls);
        Assert.Contains("Saved your app list", viewModel.MoveToNewPcMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SaveAppList_Cancelled_DoesNothing()
    {
        var viewModel = Create();

        await viewModel.SaveAppListCommand.ExecuteAsync(null);

        Assert.Empty(_wingetClient.ExportCalls);
    }

    [Fact]
    public async Task ChooseAppList_NotAnExport_ShowsMessageAndNoConfirmation()
    {
        _dialogs.OpenPath = WriteFile("hello");
        var viewModel = Create();

        await viewModel.ChooseAppListCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsImportConfirmationVisible);
        Assert.NotNull(viewModel.MoveToNewPcMessage);
        Assert.Empty(_wingetClient.ImportCalls);
    }

    [Fact]
    public async Task ChooseAppList_Valid_ShowsAppsAndDoesNotInstallYet()
    {
        _dialogs.OpenPath = WriteFile(ValidExport);
        var viewModel = Create();

        await viewModel.ChooseAppListCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsImportConfirmationVisible);
        Assert.Equal(["VideoLAN.VLC", "7zip.7zip"], viewModel.PendingImportAppIds);
        Assert.Empty(_wingetClient.ImportCalls);
    }

    [Fact]
    public async Task Cancel_ClearsConfirmation()
    {
        _dialogs.OpenPath = WriteFile(ValidExport);
        var viewModel = Create();
        await viewModel.ChooseAppListCommand.ExecuteAsync(null);

        viewModel.CancelImportCommand.Execute(null);

        Assert.False(viewModel.IsImportConfirmationVisible);
        Assert.Empty(_wingetClient.ImportCalls);
    }

    [Fact]
    public async Task Confirm_RunsImport()
    {
        var path = WriteFile(ValidExport);
        _dialogs.OpenPath = path;
        var viewModel = Create();
        await viewModel.ChooseAppListCommand.ExecuteAsync(null);

        await viewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal([path], _wingetClient.ImportCalls);
        Assert.Contains("Finished installing", viewModel.MoveToNewPcMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Confirm_FileChangedAfterConfirmation_InstallsNothing()
    {
        var path = WriteFile(ValidExport);
        _dialogs.OpenPath = path;
        var viewModel = Create();
        await viewModel.ChooseAppListCommand.ExecuteAsync(null);
        File.WriteAllText(path, "{\"Sources\":[{\"Packages\":[{\"PackageIdentifier\":\"Evil.App\"}]}]}");

        await viewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Empty(_wingetClient.ImportCalls);
        Assert.Contains("changed", viewModel.MoveToNewPcMessage);
    }

    [Fact]
    public async Task Refresh_ReportsPendingCountToTracker()
    {
        _wingetClient.UpgradeListResults.Enqueue(
        [
            new WingetPackage("A", "A.Id", "1", "2", "winget", false),
            new WingetPackage("B", "B.Id", "1", "2", "winget", false),
        ]);
        var viewModel = Create();

        await viewModel.RefreshAsync(quiet: false);

        Assert.Equal(2, _tracker.Current?.Count);
    }
}
