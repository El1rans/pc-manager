using Porchlight.App.Features.RecentChanges;
using Porchlight.App.Shell;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Changes;
using Xunit;

namespace Porchlight.App.Tests.Features.RecentChanges;

public sealed class RecentChangesViewModelTests
{
    private readonly FakeJournal _journal = new();

    private async Task<RecentChangesViewModel> LoadAsync()
    {
        var vm = new RecentChangesViewModel(_journal);
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [Fact]
    public async Task Page_IsLastInTuneUp()
    {
        var vm = await LoadAsync();

        Assert.Equal("Recent changes", vm.Title);
        Assert.Equal(PageCategory.TuneUp, vm.Category);
        Assert.True(vm.Order > 4); // after Updates, Startup apps, Free up space and System health
        Assert.True(vm.ShowEmptyState);
    }

    [Fact]
    public async Task ListsNewestFirstWithPlainLabels()
    {
        _journal.Record(ChangeArea.Cleanup, "Cleared 1 GB of junk files");
        _journal.Record(ChangeArea.Startup, "Turned off Foo at startup", "t", "p");

        var vm = await LoadAsync();

        Assert.Equal(["Turned off Foo at startup", "Cleared 1 GB of junk files"], vm.Changes.Select(c => c.Description));
        Assert.True(vm.Changes[0].CanUndo);
        Assert.False(vm.Changes[0].ShowCannotUndo);
        Assert.True(vm.Changes[1].ShowCannotUndo);
        Assert.False(vm.Changes[1].CanUndo);
        Assert.Equal("Startup apps", vm.Changes[0].AreaText);
        Assert.False(vm.ShowEmptyState);
    }

    [Fact]
    public async Task Undo_ShowsTheResultAndMarksTheRowUndone()
    {
        _journal.Record(ChangeArea.Startup, "Turned off Foo at startup", "t", "p");
        var vm = await LoadAsync();

        await vm.UndoCommand.ExecuteAsync(vm.Changes[0]);

        Assert.Equal("Done.", vm.Message);
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.Changes[0].ShowUndone);
        Assert.False(vm.Changes[0].CanUndo);
    }

    [Fact]
    public async Task Undo_FailureShowsAPlainErrorAndKeepsTheButton()
    {
        _journal.Record(ChangeArea.Services, "Stopped Foo", "t", "p");
        _journal.UndoResult = ChangeUndoResult.Fail("Changing a service needs administrator rights.");
        var vm = await LoadAsync();

        await vm.UndoCommand.ExecuteAsync(vm.Changes[0]);

        Assert.Equal("Changing a service needs administrator rights.", vm.ErrorMessage);
        Assert.Null(vm.Message);
        Assert.True(vm.Changes[0].CanUndo);
    }
}
