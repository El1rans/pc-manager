using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.RemoveApps;
using Porchlight.App.Shell;
using Porchlight.Core.Cleanup;
using Porchlight.Core.RemoveApps;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.App.Tests.Features.RemoveApps;

public sealed class RemoveAppsViewModelTests
{
    private readonly FakeService _service = new();
    private readonly FakeConfirm _confirm = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));

    private static RemovableApp App(
        string name, long? sizeKb = null, DateOnly? date = null, RemovableAppKind kind = RemovableAppKind.Normal,
        bool preinstalled = false, string? publisher = "Pub") =>
        new(new InstalledApp(name, publisher, "1.0", sizeKb is { } kb ? kb * 1024 : null, date, "x.exe", true), kind, preinstalled, null);

    private async Task<RemoveAppsViewModel> LoadAsync(params RemovableApp[] apps)
    {
        _service.Apps = [.. apps];
        var vm = new RemoveAppsViewModel(_service, _confirm, _time, NullLogger<RemoveAppsViewModel>.Instance);
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [Fact]
    public async Task Load_SplitsNormalAppsFromSystemPartsAndSortsByName()
    {
        var vm = await LoadAsync(
            App("Zed"), App("alpha"), App("Microsoft .NET Runtime", kind: RemovableAppKind.SystemPart), App("AnyDesk", kind: RemovableAppKind.ManagedByPorchlight));

        Assert.Equal("Remove apps", vm.Title);
        Assert.Equal(PageCategory.Apps, vm.Category);
        Assert.Equal(4, vm.Order);
        Assert.Equal(["alpha", "AnyDesk", "Zed"], vm.Apps.Select(a => a.Name));
        Assert.Equal(["Microsoft .NET Runtime"], vm.SystemParts.Select(a => a.Name));
        Assert.True(vm.HasSystemParts);
        Assert.Equal("System parts - usually keep (1)", vm.SystemPartsHeader);
        Assert.Equal("3 apps installed", vm.Summary);
    }

    [Fact]
    public async Task Managed_RowHasNoRemoveButton_AndPreinstalledIsHintOnly()
    {
        var vm = await LoadAsync(
            App("AnyDesk", kind: RemovableAppKind.ManagedByPorchlight), App("McAfee", preinstalled: true));

        var managed = vm.Apps.Single(a => a.Name == "AnyDesk");
        Assert.True(managed.IsManaged);
        Assert.False(managed.CanRemove);
        var trial = vm.Apps.Single(a => a.Name == "McAfee");
        Assert.True(trial.IsOftenPreinstalled);
        Assert.True(trial.CanRemove);
    }

    [Fact]
    public async Task Sort_BySizeAndDate_PutsUnknownLast()
    {
        var vm = await LoadAsync(
            App("A", sizeKb: 10, date: new DateOnly(2020, 1, 1)),
            App("B", sizeKb: null, date: null),
            App("C", sizeKb: 500, date: new DateOnly(2024, 5, 5)));

        vm.SelectedSort = RemoveAppsSort.Size;
        Assert.Equal(["C", "A", "B"], vm.Apps.Select(a => a.Name));

        vm.SelectedSort = RemoveAppsSort.Date;
        Assert.Equal(["C", "A", "B"], vm.Apps.Select(a => a.Name));

        vm.SelectedSort = RemoveAppsSort.Name;
        Assert.Equal(["A", "B", "C"], vm.Apps.Select(a => a.Name));
    }

    [Fact]
    public async Task Filter_MatchesNameOrPublisher_InBothGroups()
    {
        var vm = await LoadAsync(
            App("Photo Studio", publisher: "Acme"), App("Chat", publisher: "Photo Inc"), App("Other"),
            App("Photo Runtime", kind: RemovableAppKind.SystemPart), App("Unrelated Runtime", kind: RemovableAppKind.SystemPart));

        vm.Filter = "photo";

        Assert.Equal(["Chat", "Photo Studio"], vm.Apps.Select(a => a.Name));
        Assert.Equal(["Photo Runtime"], vm.SystemParts.Select(a => a.Name));
    }

    [Fact]
    public async Task Remove_DeclinedConfirmation_DoesNothing()
    {
        var vm = await LoadAsync(App("VLC"));
        _confirm.Answer = false;

        await vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.Empty(_service.Removed);
        Assert.Contains("can't be undone from Porchlight", Assert.Single(_confirm.Messages));
        Assert.Equal("Remove VLC?", _confirm.Titles[0]);
    }

    [Fact]
    public async Task Remove_Confirmed_RemovesThenRefreshesTheList()
    {
        var vm = await LoadAsync(App("VLC"), App("Other"));
        _service.Next = new RemoveAppOutcome(RemoveAppResult.Removed);
        _service.DropOnRemove = true;

        await vm.RemoveCommand.ExecuteAsync(vm.Apps.Single(a => a.Name == "VLC"));

        Assert.Equal(["VLC"], _service.Removed);
        Assert.Equal("VLC was removed.", vm.Message);
        Assert.Equal(["Other"], vm.Apps.Select(a => a.Name));
        Assert.False(vm.IsBusyWithWork);
    }

    [Fact]
    public async Task Remove_SystemPart_WarnsInTheConfirmation()
    {
        var vm = await LoadAsync(App("Microsoft .NET Runtime", kind: RemovableAppKind.SystemPart));

        await vm.RemoveCommand.ExecuteAsync(vm.SystemParts[0]);

        Assert.Contains(RemoveAppsViewModel.SystemPartWarning, _confirm.Messages[0]);
    }

    [Fact]
    public async Task Remove_ManagedRow_IsIgnored()
    {
        var vm = await LoadAsync(App("AnyDesk", kind: RemovableAppKind.ManagedByPorchlight));

        await vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.Empty(_confirm.Messages);
        Assert.Empty(_service.Removed);
    }

    [Fact]
    public async Task Remove_BusyWhileRunning_AndReportsBusyGuard()
    {
        var vm = await LoadAsync(App("VLC"));
        var gate = new TaskCompletionSource<RemoveAppOutcome>();
        _service.Gate = gate;

        var running = vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.True(vm.IsBusyWithWork);
        Assert.False(vm.Apps[0].CanRemoveNow);
        gate.SetResult(new RemoveAppOutcome(RemoveAppResult.UninstallerOpened));
        await running;
        Assert.False(vm.IsBusyWithWork);
        Assert.Contains("opened", vm.Message);
    }

    [Theory]
    [InlineData(RemoveAppResult.Refused, RemoveAppsViewModel.RefusedMessage)]
    [InlineData(RemoveAppResult.BlockedWhileElevated, RemoveAppsViewModel.BlockedWhileElevatedMessage)]
    [InlineData(RemoveAppResult.InvalidCommand, RemoveAppsViewModel.InvalidCommandMessage)]
    public async Task Remove_Problems_ShowAPlainErrorAndKeepTheList(RemoveAppResult result, string expected)
    {
        var vm = await LoadAsync(App("VLC"));
        _service.Next = new RemoveAppOutcome(result);

        await vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.Equal(expected, vm.ErrorMessage);
        Assert.Single(vm.Apps);
    }

    [Fact]
    public async Task Remove_Failed_MentionsThePermissionPrompt()
    {
        var vm = await LoadAsync(App("VLC"));
        _service.Next = new RemoveAppOutcome(RemoveAppResult.Failed);

        await vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.Contains("permission", vm.ErrorMessage);
    }

    [Fact]
    public async Task Remove_WingetProblem_ShowsWingetsPlainExplanation()
    {
        var vm = await LoadAsync(App("VLC"));
        var outcome = new WingetOutcome(WingetOutcomeKind.AppRunning, "Close the app and try again", "VLC is open.", 1, WingetSuggestedAction.Retry);
        _service.Next = new RemoveAppOutcome(RemoveAppResult.WingetProblem, outcome);

        await vm.RemoveCommand.ExecuteAsync(vm.Apps[0]);

        Assert.Equal("Close the app and try again. VLC is open.", vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_Failure_ShowsAMessageInsteadOfCrashing()
    {
        _service.ListException = new InvalidOperationException("boom");
        var vm = new RemoveAppsViewModel(_service, _confirm, _time, NullLogger<RemoveAppsViewModel>.Instance);

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemoveAppsViewModel.LoadFailedMessage, vm.ErrorMessage);
    }

    private sealed class FakeService : IRemoveAppsService
    {
        public List<RemovableApp> Apps { get; set; } = [];

        public List<string> Removed { get; } = [];

        public RemoveAppOutcome Next { get; set; } = new(RemoveAppResult.Removed);

        public bool DropOnRemove { get; set; }

        public Exception? ListException { get; set; }

        public TaskCompletionSource<RemoveAppOutcome>? Gate { get; set; }

        public event EventHandler<AppRemovedEventArgs>? AppRemoved
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<RemovableApp>> ListAsync(CancellationToken cancellationToken) =>
            ListException is { } ex
                ? Task.FromException<IReadOnlyList<RemovableApp>>(ex)
                : Task.FromResult<IReadOnlyList<RemovableApp>>([.. Apps]);

        public async Task<RemoveAppOutcome> RemoveAsync(RemovableApp app, CancellationToken cancellationToken)
        {
            Removed.Add(app.App.DisplayName);
            if (Gate is not null)
            {
                return await Gate.Task;
            }

            if (DropOnRemove)
            {
                Apps.Remove(app);
            }

            return Next;
        }
    }

    private sealed class FakeConfirm : IConfirmationDialog
    {
        public bool Answer { get; set; } = true;

        public List<string> Messages { get; } = [];

        public List<string> Titles { get; } = [];

        public bool Confirm(string title, string message)
        {
            Titles.Add(title);
            Messages.Add(message);
            return Answer;
        }
    }
}
