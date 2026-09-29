using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.App.Tests.Features.Cleanup;

public sealed class DuplicatesViewModelTests
{
    private const long Size = 2_000_000;
    private static readonly DateTime Base = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeDuplicateFinder _finder = new();
    private readonly FakeDuplicateRemover _remover = new();
    private readonly FakeInsightsConfirmation _confirmation = new();
    private readonly FakeInsightsProcessRunner _runner = new();

    private static DuplicateFile File(string folder, string name, int days) =>
        new($@"C:\Users\Test\{folder}\{name}", name, $@"C:\Users\Test\{folder}", Base.AddDays(days));

    private static DuplicateGroup Group(string name, params int[] days) =>
        new(Size, days.Select((d, i) => File("F" + i, name, d)).OrderByDescending(f => f.LastWriteUtc).ToList());

    private DuplicatesViewModel Create() =>
        new(_finder, _remover, new FakeInsightsPaths(), _runner, _confirmation, NullLogger<DuplicatesViewModel>.Instance);

    private async Task<DuplicatesViewModel> Scanned(params DuplicateGroup[] groups)
    {
        _finder.Groups = groups;
        var viewModel = Create();
        await viewModel.ScanCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Scan_ShowsGroupsWithWastedSpace_AndNothingTicked()
    {
        var viewModel = await Scanned(Group("Holiday.mp4", 1, 5, 9));

        var group = Assert.Single(viewModel.Groups);
        Assert.Equal("3 copies of Holiday.mp4", group.Title);
        Assert.Contains("MB could be freed", group.WastedText);
        Assert.All(group.Files, f => Assert.False(f.IsSelected));
        Assert.True(viewModel.HasScanned);
        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.MoveSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Scan_WithNoDuplicates_SaysSo()
    {
        var viewModel = await Scanned();

        Assert.Equal("No duplicate files found.", viewModel.SummaryText);
        Assert.Empty(viewModel.Groups);
    }

    [Fact]
    public async Task KeepNewest_TicksEveryCopyExceptTheNewest()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5, 9));
        var group = viewModel.Groups[0];

        group.KeepNewestCommand.Execute(null);

        Assert.Equal([false, true, true], group.Files.Select(f => f.IsSelected)); // newest first
        Assert.True(group.Files[0].IsNewest);
        Assert.True(viewModel.HasSelection);
        Assert.Contains("2 files", viewModel.SelectedText);
    }

    [Fact]
    public async Task KeepNewestInEveryGroup_AndClearTicks()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5), Group("b.bin", 2, 3));

        viewModel.KeepNewestInEveryGroupCommand.Execute(null);
        Assert.Equal(2, viewModel.Groups.Sum(g => g.SelectedFiles.Count()));

        viewModel.ClearSelectionCommand.Execute(null);
        Assert.False(viewModel.HasSelection);
    }

    [Fact]
    public async Task TheLastUntickedCopy_CannotBeTicked_SoOneCopyAlwaysStays()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5, 9));
        var files = viewModel.Groups[0].Files;

        files[0].IsSelected = true;
        files[1].IsSelected = true;

        Assert.False(files[2].CanTick);
        Assert.True(files[2].IsLastCopy);
        Assert.True(files[0].CanTick); // ticked copies can always be unticked

        files[2].IsSelected = true; // programmatic backstop

        Assert.False(files[2].IsSelected);
        Assert.Contains("at least one copy", viewModel.Message);
    }

    [Fact]
    public async Task MoveSelected_AsksFirst_ThenSendsOnlyTheTickedPaths_AndRemovesThemFromTheList()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5, 9), Group("b.bin", 2, 3));
        viewModel.KeepNewestInEveryGroupCommand.Execute(null);
        var expected = viewModel.Groups.SelectMany(g => g.SelectedFiles).Select(f => f.File.FullPath).Order().ToList();

        await viewModel.MoveSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, _confirmation.AskCount);
        Assert.Contains("3 files", _confirmation.LastMessage);
        Assert.Contains("One copy of each file stays", _confirmation.LastMessage);
        Assert.Equal(expected, _remover.LastSelected.Order());
        Assert.Empty(viewModel.Groups); // each set is down to one copy: no longer duplicates
        Assert.Contains("Moved 3 files", viewModel.ResultText);
        Assert.False(viewModel.HasSelection);
    }

    [Fact]
    public async Task MoveSelected_WhenSomeCopiesWereNotRemoved_KeepsThemListed()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5, 9));
        viewModel.Groups[0].KeepNewestCommand.Execute(null);
        var oldest = viewModel.Groups[0].Files[2].File.FullPath;
        _remover.RemovedOverride = [oldest];

        await viewModel.MoveSelectedCommand.ExecuteAsync(null);

        var group = Assert.Single(viewModel.Groups);
        Assert.Equal(2, group.Files.Count);
        Assert.DoesNotContain(group.Files, f => f.File.FullPath == oldest);
    }

    [Fact]
    public async Task MoveSelected_WhenDeclined_ChangesNothing()
    {
        _confirmation.Answer = false;
        var viewModel = await Scanned(Group("a.bin", 1, 5));
        viewModel.Groups[0].KeepNewestCommand.Execute(null);

        await viewModel.MoveSelectedCommand.ExecuteAsync(null);

        Assert.Empty(_remover.LastSelected);
        Assert.Single(viewModel.Groups);
    }

    [Fact]
    public async Task ShowInFolder_SelectsTheFileInExplorer()
    {
        var viewModel = await Scanned(Group("a.bin", 1, 5));

        viewModel.ShowInFolderCommand.Execute(viewModel.Groups[0].Files[0]);

        Assert.Equal("explorer.exe", _runner.Detached[0].FileName);
        Assert.Equal("/select,", _runner.Detached[0].Arguments[0]);
        Assert.Equal(viewModel.Groups[0].Files[0].File.FullPath, _runner.Detached[0].Arguments[1]);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var viewModel = Create();

        viewModel.Dispose();

        Assert.Null(Record.Exception(viewModel.Dispose));
    }
}
