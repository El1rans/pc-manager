using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.App.Tests.Features.Cleanup;

public sealed class DiskMapViewModelTests
{
    private const string Profile = @"C:\Users\Test";
    private const string Docs = Profile + @"\Documents";
    private const string Downloads = Profile + @"\Downloads";

    private readonly TreeFileSystem _fs = new();
    private readonly FakeInsightsRecycler _recycler = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeInsightsConfirmation _confirmation = new();
    private readonly FakeFolderPicker _picker = new();

    public DiskMapViewModelTests()
    {
        _fs.Dir(Profile)
            .Dir(Docs)
            .File(Docs + @"\big.iso", 500)
            .File(Docs + @"\small.txt", 20)
            .Dir(Downloads)
            .File(Downloads + @"\setup.exe", 100)
            .File(Profile + @"\readme.txt", 5);
    }

    private DiskMapViewModel Create()
    {
        var mapper = new DiskSpaceMapper(_fs, new FakeDocsPaths(), NullLogger<DiskSpaceMapper>.Instance);
        return new DiskMapViewModel(
            mapper, new FakeInsightsDrives(), new FakeDocsPaths(), _recycler, _runner, _picker, _confirmation,
            NullLogger<DiskMapViewModel>.Instance);
    }

    private static async Task Scan(DiskMapViewModel viewModel) =>
        await viewModel.ScanCommand.ExecuteAsync(null);

    /// <summary>Personal folders include Documents so files there may be recycled.</summary>
    private sealed class FakeDocsPaths : ICleanupPathProvider
    {
        public string TempPath => @"C:\T";

        public string WindowsDirectory => @"C:\Windows";

        public string LocalAppData => @"C:\U\AppData\Local";

        public string ProgramData => @"C:\ProgramData";

        public string UserProfile => Profile;

        public string ProgramFiles => @"C:\Program Files";

        public string ProgramFilesX86 => @"C:\Program Files (x86)";

        public string DownloadsFolder => Downloads;

        public IReadOnlyList<string> PersonalFolders => [Docs];
    }

    [Fact]
    public void Locations_StartWithYourFiles_ThenEachDrive()
    {
        var viewModel = Create();

        Assert.Equal(["Your files", "Windows (C:)", "Drive D:"], viewModel.Locations.Select(l => l.Label));
        Assert.Equal("Your files", viewModel.SelectedLocation?.Label);
    }

    [Fact]
    public async Task Scan_ShowsBiggestFirst_WithSizesAndPercentages()
    {
        var viewModel = Create();

        await Scan(viewModel);

        Assert.True(viewModel.HasResult);
        Assert.Equal(["Documents", "Downloads", "readme.txt"], viewModel.Rows.Select(r => r.Name));
        Assert.Equal(83, Math.Round(viewModel.Rows[0].Percent));
        Assert.Equal("83%", viewModel.Rows[0].PercentText);
        Assert.Contains("Documents", viewModel.Rows[0].AutomationName);
        Assert.Contains("625 B", viewModel.SummaryText);
        Assert.Equal(["Your files"], viewModel.Breadcrumbs.Select(b => b.Label));
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task OpenRow_DrillsIn_AndTheBreadcrumbGoesBackUp()
    {
        var viewModel = Create();
        await Scan(viewModel);

        viewModel.OpenRowCommand.Execute(viewModel.Rows[0]);

        Assert.Equal(["Your files", "Documents"], viewModel.Breadcrumbs.Select(b => b.Label));
        Assert.Equal(["big.iso", "small.txt"], viewModel.Rows.Select(r => r.Name));
        Assert.True(viewModel.Breadcrumbs[0].IsLink);
        Assert.True(viewModel.Breadcrumbs[1].IsCurrent);

        viewModel.GoToCommand.Execute(viewModel.Breadcrumbs[0]);

        Assert.Single(viewModel.Breadcrumbs);
        Assert.Equal("Documents", viewModel.Rows[0].Name);
    }

    [Fact]
    public async Task Rows_AreCappedAtTheLimit_WithAnEverythingElseLine()
    {
        for (var i = 1; i <= DiskMapViewModel.MaxRows + 5; i++)
        {
            _fs.Dir(Profile + $@"\d{i}").File(Profile + $@"\d{i}\f.bin", 1000 + i);
        }

        var viewModel = Create();
        await Scan(viewModel);

        Assert.Equal(DiskMapViewModel.MaxRows + 1, viewModel.Rows.Count);
        var other = viewModel.Rows[^1];
        Assert.Equal("Everything else", other.Name);
        Assert.False(other.CanOpen);
        Assert.False(other.CanShowInFolder);
        Assert.False(other.CanRecycle);
    }

    [Fact]
    public async Task OnlyFilesInPersonalFolders_OfferTheRecycleBin()
    {
        var viewModel = Create();
        await Scan(viewModel);

        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Documents"));
        Assert.All(viewModel.Rows, r => Assert.True(r.CanRecycle));

        viewModel.GoToCommand.Execute(viewModel.Breadcrumbs[0]);
        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Downloads"));
        Assert.All(viewModel.Rows, r => Assert.False(r.CanRecycle));

        viewModel.GoToCommand.Execute(viewModel.Breadcrumbs[0]);
        Assert.False(viewModel.Rows.Single(r => r.Name == "readme.txt").CanRecycle);
        Assert.False(viewModel.Rows.Single(r => r.Name == "Documents").CanRecycle); // folders never
    }

    [Fact]
    public async Task MoveToRecycleBin_AsksFirst_ThenRemovesTheRowAndShrinksTheTotals()
    {
        var viewModel = Create();
        await Scan(viewModel);
        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Documents"));
        var big = viewModel.Rows.Single(r => r.Name == "big.iso");

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(big);

        Assert.Equal(1, _confirmation.AskCount);
        Assert.Equal([Docs + @"\big.iso"], _recycler.Moved);
        Assert.Equal(["small.txt"], viewModel.Rows.Select(r => r.Name));
        Assert.Contains("20 B", viewModel.SummaryText);
        viewModel.GoToCommand.Execute(viewModel.Breadcrumbs[0]);
        Assert.Contains("125 B", viewModel.SummaryText);
    }

    [Fact]
    public async Task MoveToRecycleBin_WhenDeclined_DoesNothing()
    {
        _confirmation.Answer = false;
        var viewModel = Create();
        await Scan(viewModel);
        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Documents"));

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.Rows[0]);

        Assert.Empty(_recycler.Moved);
        Assert.Equal(2, viewModel.Rows.Count);
    }

    [Fact]
    public async Task MoveToRecycleBin_ForAFileOutsideThePersonalFolders_IsIgnored()
    {
        var viewModel = Create();
        await Scan(viewModel);
        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Downloads"));

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.Rows[0]);

        Assert.Equal(0, _confirmation.AskCount);
        Assert.Empty(_recycler.Moved);
    }

    [Fact]
    public async Task MoveToRecycleBin_WhenTheRecyclerRefuses_KeepsTheRowAndExplains()
    {
        _recycler.Succeeds = false;
        var viewModel = Create();
        await Scan(viewModel);
        viewModel.OpenRowCommand.Execute(viewModel.Rows.Single(r => r.Name == "Documents"));

        await viewModel.MoveToRecycleBinCommand.ExecuteAsync(viewModel.Rows[0]);

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Contains("could not move", viewModel.Message);
    }

    [Fact]
    public async Task ShowInFolder_OpensAFolderDirectly_AndSelectsAFileInExplorer()
    {
        var viewModel = Create();
        await Scan(viewModel);

        viewModel.ShowInFolderCommand.Execute(viewModel.Rows.Single(r => r.Name == "Documents"));
        viewModel.ShowInFolderCommand.Execute(viewModel.Rows.Single(r => r.Name == "readme.txt"));

        Assert.Equal("explorer.exe", _runner.StartDetachedCalls[0].FileName);
        Assert.Equal([Docs], _runner.StartDetachedCalls[0].Arguments);
        Assert.Equal("/select,", _runner.StartDetachedCalls[1].Arguments[0]);
        Assert.Equal(Profile + @"\readme.txt", _runner.StartDetachedCalls[1].Arguments[1]);
    }

    [Fact]
    public async Task Scan_ReportsFoldersThatCouldNotBeRead()
    {
        _fs.Unreadable(Downloads);
        var viewModel = Create();

        await Scan(viewModel);

        Assert.Contains("Couldn't read 1 folder", viewModel.CouldntReadText);
    }

    [Fact]
    public async Task Scan_OfAMissingFolder_ExplainsInPlainLanguage()
    {
        _picker.Choice = @"C:\Nowhere";
        var viewModel = Create();
        viewModel.ChooseFolderCommand.Execute(null);

        await Scan(viewModel);

        Assert.Contains("could not find that folder", viewModel.Message);
        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public void ChooseFolder_AddsAndSelectsTheFolder_AndIgnoresCancel()
    {
        var viewModel = Create();
        _picker.Choice = null;
        viewModel.ChooseFolderCommand.Execute(null);
        Assert.Equal("Your files", viewModel.SelectedLocation?.Label);

        _picker.Choice = @"C:\Data\Photos";
        viewModel.ChooseFolderCommand.Execute(null);

        Assert.Equal("Photos", viewModel.SelectedLocation?.Label);
        Assert.Equal(@"C:\Data\Photos", viewModel.SelectedLocation?.Path);
    }

    [Fact]
    public async Task Scan_StartsOnlyWhenAsked_AndUsesTheSelectedLocation()
    {
        var tree = await new DiskSpaceMapper(_fs, new FakeDocsPaths(), NullLogger<DiskSpaceMapper>.Instance)
            .MapAsync(Profile, null, TestContext.Current.CancellationToken);
        var mapper = new FakeDiskMapper { Result = tree };
        var viewModel = new DiskMapViewModel(
            mapper, new FakeInsightsDrives(), new FakeDocsPaths(), _recycler, _runner, _picker, _confirmation,
            NullLogger<DiskMapViewModel>.Instance);
        Assert.Null(mapper.LastRoot);

        viewModel.SelectedLocation = viewModel.Locations[1];
        await Scan(viewModel);

        Assert.Equal(@"C:\", mapper.LastRoot);
    }
}
