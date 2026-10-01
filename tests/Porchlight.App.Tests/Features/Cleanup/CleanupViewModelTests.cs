using Porchlight.App.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Elevation;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.App.Tests.Features.Cleanup;

public sealed class CleanupViewModelTests
{
    private readonly FakeCatalog _catalog = new();
    private readonly FakeScanner _scanner = new();
    private readonly FakeRunner _runner = new();
    private readonly FakeFinder _finder = new();
    private readonly FakeRecycler _recycler = new();
    private readonly FakeAppsReader _apps = new();
    private readonly FakeUninstaller _uninstaller = new();
    private readonly FakeElevationService _elevation = new();
    private readonly FakeProcessRunner _processRunner = new();
    private readonly FakeUrlLauncher _urls = new();
    private readonly FakeConfirmation _confirmation = new();
    private readonly FakeSettingsStore _settings = new();

    public CleanupViewModelTests()
    {
        _catalog.Categories =
        [
            Cat(CleanupCategoryId.TemporaryFiles, admin: false, selected: true),
            Cat(CleanupCategoryId.WindowsUpdateLeftovers, admin: true, selected: true),
            Cat(CleanupCategoryId.RecycleBin, admin: false, selected: false),
        ];
    }

    private static CleanupCategory Cat(CleanupCategoryId id, bool admin, bool selected) =>
        new(id, id.ToString(), "desc", admin, selected, id == CleanupCategoryId.RecycleBin ? [] : [new CleanupRoot(@"C:\x")], null, TimeSpan.Zero);

    private CleanupViewModel Create() =>
        new(new FakePaths(), _catalog, _scanner, _runner, _finder, _recycler, _apps, _uninstaller, new FakeDrives(),
            _elevation, _processRunner, _urls, _confirmation, _settings,
            new DiskMapViewModel(
                new FakeDiskMapper(), new FakeInsightsDrives(), new FakeInsightsPaths(), new FakeInsightsRecycler(),
                new FakeProcessRunner(), new FakeFolderPicker(), new FakeInsightsConfirmation(),
                NullLogger<DiskMapViewModel>.Instance),
            new DuplicatesViewModel(
                new FakeDuplicateFinder(), new FakeDuplicateRemover(), new FakeInsightsPaths(),
                new FakeProcessRunner(), new FakeInsightsConfirmation(), NullLogger<DuplicatesViewModel>.Instance),
            NullLogger<CleanupViewModel>.Instance);

    private async Task<CleanupViewModel> CreateScanned()
    {
        var viewModel = Create();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return viewModel;
    }

    private static FoundFile File(string name = "big.bin") => new(@"C:\Docs\" + name, name, @"C:\Docs", 900_000_000, DateTime.UtcNow);

    private static InstalledApp App(bool perMachine = true) => new("App", "Pub", "1", null, null, "u.exe", perMachine);

    [Fact]
    public void Page_HasTheSpecifiedTitleGlyphAndOrder()
    {
        var viewModel = Create();

        Assert.Equal("Free up space", viewModel.Title);
        Assert.Equal("\uE74D", viewModel.Glyph);
        Assert.Equal(3, viewModel.Order);
        Assert.Equal(PageCategory.TuneUp, viewModel.Category);
    }

    [Fact]
    public async Task Scan_ShowsEveryCategory_RecycleBinUnticked_AdminOnlyDisabledWhenNotElevated()
    {
        var viewModel = await CreateScanned();

        Assert.Equal(3, viewModel.Categories.Count);
        var update = viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.WindowsUpdateLeftovers);
        Assert.False(update.IsEnabled);
        Assert.False(update.IsSelected);
        Assert.Contains("administrator", update.Note, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.RecycleBin).IsSelected);
        Assert.True(viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.TemporaryFiles).IsSelected);
        Assert.True(viewModel.ShowAdminBanner);
        Assert.DoesNotContain(_scanner.Scanned, c => c.Id == CleanupCategoryId.WindowsUpdateLeftovers);
    }

    [Fact]
    public async Task Scan_WhenElevated_EnablesAdminCategoriesAndHidesBanner()
    {
        _elevation.IsElevated = true;

        var viewModel = await CreateScanned();

        Assert.All(viewModel.Categories, r => Assert.True(r.IsEnabled));
        Assert.False(viewModel.ShowAdminBanner);
    }

    [Fact]
    public async Task Scan_ShowsSizesAndSelectedTotal()
    {
        _scanner.BytesFor[CleanupCategoryId.TemporaryFiles] = 1024L * 1024 * 1024;

        var viewModel = await CreateScanned();

        Assert.Equal("1.0 GB", viewModel.Categories[0].SizeText);
        Assert.Contains("1.0 GB", viewModel.SelectedTotalText);
        Assert.True(viewModel.CleanCommand.CanExecute(null));
    }

    [Fact]
    public async Task Scan_LoadsFilesDownloadsAndApps()
    {
        _finder.BigFiles = [File("a.bin")];
        _finder.OldDownloads = [File("setup.exe")];
        _apps.Apps = [App()];

        var viewModel = await CreateScanned();

        Assert.Single(viewModel.BigFiles);
        Assert.Single(viewModel.OldDownloads);
        Assert.Single(viewModel.Apps);
    }

    [Fact]
    public async Task Apps_ShowTop25_UntilShowAll()
    {
        _apps.Apps = Enumerable.Range(0, 40).Select(_ => App()).ToList();

        var viewModel = await CreateScanned();
        Assert.Equal(25, viewModel.Apps.Count);
        Assert.True(viewModel.CanShowAllApps);

        viewModel.ShowAllAppsCommand.Execute(null);
        Assert.Equal(40, viewModel.Apps.Count);
        Assert.False(viewModel.CanShowAllApps);
    }

    [Fact]
    public async Task Clean_PassesOnlyTickedCategories_AndRemembersTotalFreed()
    {
        _runner.Result = new CleanupRunResult(2048, 3, 41, false, []);
        var viewModel = await CreateScanned();

        await viewModel.CleanCommand.ExecuteAsync(null);

        var cleaned = Assert.Single(_runner.Cleaned);
        Assert.Equal(CleanupCategoryId.TemporaryFiles, cleaned.Id);
        Assert.Equal("Freed 2.0 KB. 41 files were in use and left alone.", viewModel.ResultText);
        Assert.Equal(2048, _settings.Current.Cleanup.TotalBytesFreed);
        Assert.NotNull(_settings.Current.Cleanup.LastCleanedUtc);
        Assert.Contains("has freed", viewModel.FreedSoFarText);
    }

    [Fact]
    public async Task Clean_WithRecycleBinTicked_AsksFirst_AndDoesNothingIfDeclined()
    {
        var viewModel = await CreateScanned();
        viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.RecycleBin).IsSelected = true;
        _confirmation.Answer = false;

        await viewModel.CleanCommand.ExecuteAsync(null);

        Assert.Equal(1, _confirmation.AskCount);
        Assert.Empty(_runner.Cleaned);
    }

    [Fact]
    public async Task Clean_WithoutRecycleBin_DoesNotAsk()
    {
        var viewModel = await CreateScanned();

        await viewModel.CleanCommand.ExecuteAsync(null);

        Assert.Equal(0, _confirmation.AskCount);
        Assert.Single(_runner.Cleaned);
    }

    [Fact]
    public async Task Clean_NothingTicked_CannotRun()
    {
        var viewModel = await CreateScanned();
        foreach (var row in viewModel.Categories)
        {
            row.IsSelected = false;
        }

        Assert.False(viewModel.CleanCommand.CanExecute(null));
    }

    [Fact]
    public async Task UntickingADefaultOnCategory_IsRemembered_AndRestoredOnNextScan()
    {
        var viewModel = await CreateScanned();
        viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.TemporaryFiles).IsSelected = false;
        Assert.Contains("TemporaryFiles", _settings.Current.Cleanup.DeselectedCategories);

        await viewModel.ScanCommand.ExecuteAsync(null);

        Assert.False(viewModel.Categories.Single(r => r.Category.Id == CleanupCategoryId.TemporaryFiles).IsSelected);
    }

    [Fact]
    public async Task MoveToRecycleBin_Declined_DoesNothing()
    {
        _finder.BigFiles = [File()];
        var viewModel = await CreateScanned();
        _confirmation.Answer = false;

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.BigFiles[0]);

        Assert.Empty(_recycler.Moved);
        Assert.Single(viewModel.BigFiles);
    }

    [Fact]
    public async Task MoveToRecycleBin_Confirmed_RecyclesAndRemovesTheRow()
    {
        _finder.BigFiles = [File()];
        var viewModel = await CreateScanned();

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.BigFiles[0]);

        Assert.Equal([@"C:\Docs\big.bin"], _recycler.Moved);
        Assert.Empty(viewModel.BigFiles);
        Assert.Contains("big.bin", _confirmation.LastMessage);
    }

    [Fact]
    public async Task MoveToRecycleBin_Failure_KeepsTheRowAndExplains()
    {
        _finder.BigFiles = [File()];
        _recycler.Succeeds = false;
        var viewModel = await CreateScanned();

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.BigFiles[0]);

        Assert.Single(viewModel.BigFiles);
        Assert.NotEmpty(viewModel.FilesMessage);
    }

    [Fact]
    public async Task ShowInFolder_OpensExplorerWithTheFileSelected()
    {
        _finder.BigFiles = [File()];
        var viewModel = await CreateScanned();

        viewModel.ShowInFolderCommand.Execute(viewModel.BigFiles[0]);

        var call = Assert.Single(_processRunner.StartDetachedCalls);
        Assert.Equal("explorer.exe", call.FileName);
        Assert.Equal(["/select,", @"C:\Docs\big.bin"], call.Arguments);
    }

    [Fact]
    public async Task Uninstall_AsksFirst_ThenStartsTheUninstaller()
    {
        _apps.Apps = [App()];
        var viewModel = await CreateScanned();

        viewModel.UninstallCommand.Execute(viewModel.Apps[0]);

        Assert.Equal(1, _confirmation.AskCount);
        Assert.Single(_uninstaller.Started);
    }

    [Fact]
    public async Task Uninstall_Declined_StartsNothing()
    {
        _apps.Apps = [App()];
        var viewModel = await CreateScanned();
        _confirmation.Answer = false;

        viewModel.UninstallCommand.Execute(viewModel.Apps[0]);

        Assert.Empty(_uninstaller.Started);
    }

    [Fact]
    public async Task PerUserApp_WhileElevated_OffersOpenInstalledAppsInsteadOfUninstall()
    {
        _elevation.IsElevated = true;
        _uninstaller.BlockPerUserWhenElevated = true;
        _apps.Apps = [App(perMachine: false), App(perMachine: true)];

        var viewModel = await CreateScanned();

        Assert.False(viewModel.Apps[0].CanUninstall);
        Assert.True(viewModel.Apps[1].CanUninstall);
        viewModel.OpenInstalledAppsCommand.Execute(null);
        Assert.Equal(["ms-settings:appsfeatures"], _urls.OpenedUrls);
    }

    [Fact]
    public void OpenStorageSettings_UsesTheStorageSenseLink()
    {
        Create().OpenStorageSettingsCommand.Execute(null);

        Assert.Equal(["ms-settings:storagesense"], _urls.OpenedUrls);
    }

    [Fact]
    public async Task IsBusyWithWork_IsTrueOnlyWhileCleaning()
    {
        var gate = new TaskCompletionSource<CleanupRunResult>();
        _runner.Gate = gate;
        var viewModel = await CreateScanned();
        Assert.False(viewModel.IsBusyWithWork);

        var cleaning = viewModel.CleanCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsBusyWithWork);
        Assert.True(viewModel.StopCommand.CanExecute(null));

        gate.SetResult(new CleanupRunResult(0, 0, 0, false, []));
        await cleaning;
        Assert.False(viewModel.IsBusyWithWork);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var viewModel = Create();
        viewModel.Dispose();

        Assert.Null(Record.Exception(viewModel.Dispose));
    }

    // ---------------------------------------------------------------- fakes

    private sealed class FakePaths : ICleanupPathProvider
    {
        public string TempPath => @"C:\t";
        public string WindowsDirectory => @"C:\Windows";
        public string LocalAppData => @"C:\l";
        public string ProgramData => @"C:\p";
        public string UserProfile => @"C:\u";
        public string ProgramFiles => @"C:\pf";
        public string ProgramFilesX86 => @"C:\pf86";
        public string DownloadsFolder => @"C:\u\Downloads";
        public IReadOnlyList<string> PersonalFolders => [@"C:\u\Documents"];
    }

    private sealed class FakeCatalog : ICleanupCatalog
    {
        public IReadOnlyList<CleanupCategory> Categories { get; set; } = [];

        public IReadOnlyList<CleanupCategory> GetCategories() => Categories;
    }

    private sealed class FakeScanner : ICleanupScanner
    {
        public Dictionary<CleanupCategoryId, long> BytesFor { get; } = [];

        public List<CleanupCategory> Scanned { get; } = [];

        public Task<CleanupScanResult> ScanAsync(
            IReadOnlyList<CleanupCategory> categories, IProgress<CleanupProgress>? progress, CancellationToken cancellationToken)
        {
            Scanned.AddRange(categories);
            var result = categories
                .Select(c => new CleanupCategoryScan(c.Id, BytesFor.GetValueOrDefault(c.Id), 1, []))
                .ToList();
            return Task.FromResult(new CleanupScanResult(result));
        }
    }

    private sealed class FakeRunner : ICleanupRunner
    {
        public List<CleanupCategory> Cleaned { get; } = [];

        public CleanupRunResult Result { get; set; } = new(0, 0, 0, false, []);

        public TaskCompletionSource<CleanupRunResult>? Gate { get; set; }

        public Task<CleanupRunResult> CleanAsync(
            IReadOnlyList<CleanupCategory> categories, IProgress<CleanupProgress>? progress, CancellationToken cancellationToken)
        {
            Cleaned.AddRange(categories);
            return Gate?.Task ?? Task.FromResult(Result);
        }
    }

    private sealed class FakeFinder : ILargeFileFinder
    {
        public IReadOnlyList<FoundFile> BigFiles { get; set; } = [];

        public IReadOnlyList<FoundFile> OldDownloads { get; set; } = [];

        public Task<IReadOnlyList<FoundFile>> FindAsync(LargeFileSearchOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(options.Extensions is null ? BigFiles : OldDownloads);
    }

    private sealed class FakeRecycler : IRecycler
    {
        public bool Succeeds { get; set; } = true;

        public List<string> Moved { get; } = [];

        public bool MoveToRecycleBin(string path)
        {
            Moved.Add(path);
            return Succeeds;
        }
    }

    private sealed class FakeAppsReader : IInstalledAppsReader
    {
        public IReadOnlyList<InstalledApp> Apps { get; set; } = [];

        public IReadOnlyList<InstalledApp> GetInstalledApps() => Apps;
    }

    private sealed class FakeUninstaller : IAppUninstaller
    {
        public bool BlockPerUserWhenElevated { get; set; }

        public List<InstalledApp> Started { get; } = [];

        public bool CanStartUninstall(InstalledApp app) => !(BlockPerUserWhenElevated && !app.IsPerMachine);

        public UninstallStartResult StartUninstall(InstalledApp app)
        {
            Started.Add(app);
            return UninstallStartResult.Started;
        }
    }

    private sealed class FakeDrives : IDriveMonitor
    {
        public IReadOnlyList<DriveSnapshot> GetDrives() => [new(@"C:\", null, "NTFS", 1000, 400, false)];
    }

    private sealed class FakeConfirmation : IConfirmationDialog
    {
        public bool Answer { get; set; } = true;

        public int AskCount { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public bool Confirm(string title, string message)
        {
            AskCount++;
            LastMessage = message;
            return Answer;
        }
    }
}
