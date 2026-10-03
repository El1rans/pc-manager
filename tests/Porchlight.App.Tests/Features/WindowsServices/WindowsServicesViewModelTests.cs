using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.WindowsServices;
using Porchlight.App.Shell;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Changes;
using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.App.Tests.Features.WindowsServices;

public sealed class WindowsServicesViewModelTests
{
    private readonly FakeService _service = new();
    private readonly FakeShell _shell = new() { IsElevated = true };
    private readonly FakeConfirm _confirm = new();

    private WindowsServicesViewModel Create() =>
        new(_service, _shell, _confirm, NullLogger<WindowsServicesViewModel>.Instance);

    private static WindowsServiceEntry Entry(
        string name, bool microsoft = false, bool changeable = true, ServiceRunState state = ServiceRunState.Running,
        ServiceStartType start = ServiceStartType.Automatic, string description = "Does things.", string? publisher = "Foo Inc") =>
        new(name, name + " display", description, publisher, start, state, microsoft, changeable && !microsoft);

    private async Task<WindowsServicesViewModel> LoadAsync(params WindowsServiceEntry[] entries)
    {
        _service.Entries = entries;
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [Fact]
    public async Task Load_DefaultHidesMicrosoftServicesAndSummarisesTheOthers()
    {
        var vm = await LoadAsync(
            Entry("A"), Entry("B", state: ServiceRunState.Stopped), Entry("Win", microsoft: true));

        Assert.Equal("Services", vm.Title);
        Assert.Equal(PageCategory.Apps, vm.Category);
        Assert.Equal(3, vm.Order);
        Assert.Equal(["A", "B"], vm.Services.Select(s => s.Name));
        Assert.Equal("2 services from other apps - 1 running", vm.Summary);
    }

    [Fact]
    public async Task ShowWindowsServices_AddsMicrosoftRowsReadOnly()
    {
        var vm = await LoadAsync(Entry("A"), Entry("Win", microsoft: true));

        vm.ShowWindowsServices = true;

        Assert.Equal(2, vm.Services.Count);
        var win = vm.Services.Single(s => s.Name == "Win");
        Assert.False(win.CanChange);
        Assert.False(win.CanStop);

        vm.ShowWindowsServices = false;
        Assert.Single(vm.Services);
    }

    [Fact]
    public async Task Filter_MatchesNamePublisherAndDescription()
    {
        var vm = await LoadAsync(
            Entry("Alpha"), Entry("Beta", publisher: "Contoso"), Entry("Gamma", description: "Syncs photos."));

        vm.Filter = "contoso";
        Assert.Equal(["Beta"], vm.Services.Select(s => s.Name));

        vm.Filter = "photos";
        Assert.Equal(["Gamma"], vm.Services.Select(s => s.Name));

        vm.Filter = "alpha";
        Assert.Equal(["Alpha"], vm.Services.Select(s => s.Name));

        vm.Filter = "zzz";
        Assert.Empty(vm.Services);
        Assert.True(vm.ShowEmptyState);
    }

    [Fact]
    public async Task Row_ShowsPlainFallbacksForMissingDescriptionAndPublisher()
    {
        var vm = await LoadAsync(Entry("A", description: "", publisher: null));

        Assert.Equal("No description", vm.Services[0].Description);
        Assert.Equal("Unknown publisher", vm.Services[0].Publisher);
    }

    [Fact]
    public async Task Banner_OnlyWhenNotElevatedAndAChangeableServiceIsListed()
    {
        _shell.IsElevated = false;
        var vm = await LoadAsync(Entry("A"), Entry("Win", microsoft: true));
        Assert.True(vm.ShowAdminBanner);
        Assert.False(vm.Services[0].CanChange);
        Assert.False(vm.Services[0].CanStart);
        Assert.True(vm.Services[0].ShowNeedsAdmin);

        // Only Windows services are listed (hidden): nothing to change, so no banner.
        var onlyWindows = await LoadAsync(Entry("Win", microsoft: true));
        Assert.False(onlyWindows.ShowAdminBanner);

        _shell.IsElevated = true;
        var elevated = await LoadAsync(Entry("A"));
        Assert.False(elevated.ShowAdminBanner);
        Assert.True(elevated.Services[0].CanChange);
    }

    [Fact]
    public async Task Stop_AsksFirstAndDeclineDoesNothing()
    {
        var vm = await LoadAsync(Entry("A"));
        _confirm.Answer = false;

        await vm.StopCommand.ExecuteAsync(vm.Services[0]);

        Assert.Empty(_service.Calls);
        Assert.Single(_confirm.Messages);
    }

    [Fact]
    public async Task Stop_Confirmed_StopsAndReloads()
    {
        var vm = await LoadAsync(Entry("A"));
        _service.OnChange = () => _service.Entries = [Entry("A", state: ServiceRunState.Stopped)];

        await vm.StopCommand.ExecuteAsync(vm.Services[0]);

        Assert.Equal(["stop A"], _service.Calls);
        Assert.Equal(ServiceRunState.Stopped, vm.Services[0].State);
        Assert.Equal("A display stopped.", vm.Message);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Stop_WithDependents_NamesThem()
    {
        var vm = await LoadAsync(Entry("A"));
        _service.Next = new ServiceChangeOutcome(ServiceChangeResult.HasDependents, ["Helper one", "Helper two"]);

        await vm.StopCommand.ExecuteAsync(vm.Services[0]);

        Assert.Contains("Helper one, Helper two", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_OnlyEnabledWhenStopped_AndReportsFailure()
    {
        var vm = await LoadAsync(Entry("A", state: ServiceRunState.Stopped), Entry("B"));
        Assert.True(vm.Services[0].CanStart);
        Assert.False(vm.Services[0].CanStop);
        Assert.False(vm.Services[1].CanStart);
        _service.Next = ServiceChangeOutcome.Of(ServiceChangeResult.TimedOut);

        await vm.StartCommand.ExecuteAsync(vm.Services[0]);

        Assert.Contains("didn't respond in time", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restart_Confirmed_Restarts()
    {
        var vm = await LoadAsync(Entry("A"));

        await vm.RestartCommand.ExecuteAsync(vm.Services[0]);

        Assert.Equal(["restart A"], _service.Calls);
    }

    [Fact]
    public async Task StartType_SelectionIsApplied()
    {
        var vm = await LoadAsync(Entry("A"));
        var row = vm.Services[0];

        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await row.PendingChange;

        Assert.Equal([$"type A {ServiceStartType.Manual}"], _service.Calls);
    }

    [Fact]
    public async Task StartType_FailureRevertsTheCombo()
    {
        var vm = await LoadAsync(Entry("A", start: ServiceStartType.Automatic));
        var row = vm.Services[0];
        _service.Next = ServiceChangeOutcome.Of(ServiceChangeResult.Failed);

        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await row.PendingChange;

        Assert.Equal(ServiceStartType.Automatic, row.SelectedStartOption!.Type);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Single(_service.Calls);
    }

    [Fact]
    public async Task StartType_TurningOffAsksAndDeclineReverts()
    {
        var vm = await LoadAsync(Entry("A", start: ServiceStartType.Automatic));
        var row = vm.Services[0];
        _confirm.Answer = false;

        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.Disabled);
        await row.PendingChange;

        Assert.Empty(_service.Calls);
        Assert.Equal(ServiceStartType.Automatic, row.SelectedStartOption!.Type);
        Assert.Single(_confirm.Messages);
    }

    [Fact]
    public async Task StartType_DelayedAutomaticShowsAsItsOwnOptionWithoutApplying()
    {
        var vm = await LoadAsync(Entry("A", start: ServiceStartType.AutomaticDelayed));

        Assert.Equal(ServiceStartType.AutomaticDelayed, vm.Services[0].SelectedStartOption!.Type);
        Assert.Empty(_service.Calls);
    }

    [Fact]
    public void StartTypeOptions_OfferFourChoicesIncludingDelayed()
    {
        Assert.Equal(
            [ServiceStartType.Automatic, ServiceStartType.AutomaticDelayed, ServiceStartType.Manual, ServiceStartType.Disabled],
            StartTypeOption.All.Select(o => o.Type));
        Assert.Equal("Starts with Windows (delayed)", StartTypeOption.For(ServiceStartType.AutomaticDelayed).Label);
    }

    [Fact]
    public async Task StartType_CanSelectDelayedFromAutomaticAndBack()
    {
        var vm = await LoadAsync(Entry("A", start: ServiceStartType.Automatic));
        var row = vm.Services[0];

        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.AutomaticDelayed);
        await row.PendingChange;
        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.Automatic);
        await row.PendingChange;

        Assert.Equal([$"type A {ServiceStartType.AutomaticDelayed}", $"type A {ServiceStartType.Automatic}"], _service.Calls);
    }

    private async Task<WindowsServicesViewModel> LoadWithAsync(
        IChangeJournal journal, IAutoRestorePoint? restore, params WindowsServiceEntry[] entries)
    {
        _service.Entries = entries;
        var vm = new WindowsServicesViewModel(
            _service, _shell, _confirm, NullLogger<WindowsServicesViewModel>.Instance, journal, restore);
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [Fact]
    public async Task StartType_RecordsAnUndoableChangeWithThePreviousType()
    {
        var journal = new FakeJournal();
        var vm = await LoadWithAsync(journal, new FakeRestore(), Entry("A", start: ServiceStartType.AutomaticDelayed));
        var row = vm.Services[0];

        row.SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await row.PendingChange;

        var recorded = Assert.Single(journal.Recorded);
        Assert.Equal(ChangeArea.Services, recorded.Area);
        Assert.Equal(ServiceStartTypeUndoer.Type, recorded.UndoType);
        var payload = ChangeUndoPayload.TryDeserialize<ServiceStartTypeUndoer.Payload>(recorded.UndoPayload);
        Assert.Equal(ServiceStartType.AutomaticDelayed, payload!.StartType);
    }

    [Fact]
    public async Task StartType_AsksForARestorePointFirstAndShowsItsNote()
    {
        var restore = new FakeRestore { Result = new AutoRestorePointResult(AutoRestorePointOutcome.Created, "Restore point made.") };
        var vm = await LoadWithAsync(new FakeJournal(), restore, Entry("A"));

        vm.Services[0].SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await vm.Services[0].PendingChange;

        Assert.Equal(1, restore.Calls);
        Assert.Contains("Restore point made.", vm.Message);
    }

    [Fact]
    public async Task StartType_AFailedOrSkippedRestorePointNeverBlocksTheChange()
    {
        var restore = new FakeRestore { Result = new AutoRestorePointResult(AutoRestorePointOutcome.Failed, null) };
        var vm = await LoadWithAsync(new FakeJournal(), restore, Entry("A"));

        vm.Services[0].SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await vm.Services[0].PendingChange;

        Assert.Equal([$"type A {ServiceStartType.Manual}"], _service.Calls);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task StartType_NotElevatedDoesNotAskForARestorePoint()
    {
        _shell.IsElevated = false;
        var restore = new FakeRestore();
        var vm = await LoadWithAsync(new FakeJournal(), restore, Entry("A"));

        vm.Services[0].SelectedStartOption = StartTypeOption.For(ServiceStartType.Manual);
        await vm.Services[0].PendingChange;

        Assert.Equal(0, restore.Calls);
    }

    [Fact]
    public async Task StopAndStart_RecordUndoThatDoesTheOpposite()
    {
        var journal = new FakeJournal();
        var vm = await LoadWithAsync(journal, null, Entry("A"));

        await vm.StopCommand.ExecuteAsync(vm.Services[0]);
        await vm.StartCommand.ExecuteAsync(vm.Services[0]);

        Assert.Equal([ServiceStateUndoer.Type, ServiceStateUndoer.Type], journal.Recorded.Select(r => r.UndoType));
        Assert.True(ChangeUndoPayload.TryDeserialize<ServiceStateUndoer.Payload>(journal.Recorded[0].UndoPayload)!.Running);
        Assert.False(ChangeUndoPayload.TryDeserialize<ServiceStateUndoer.Payload>(journal.Recorded[1].UndoPayload)!.Running);
    }

    [Fact]
    public async Task FailedChange_RecordsNothing()
    {
        var journal = new FakeJournal();
        var vm = await LoadWithAsync(journal, null, Entry("A"));
        _service.Next = ServiceChangeOutcome.Of(ServiceChangeResult.Failed);

        await vm.StopCommand.ExecuteAsync(vm.Services[0]);

        Assert.Empty(journal.Recorded);
    }

    [Fact]
    public async Task Load_Failure_ShowsFriendlyError()
    {
        _service.ThrowOnList = true;
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(vm.ShowEmptyState);
    }

    private sealed class FakeRestore : IAutoRestorePoint
    {
        public int Calls { get; private set; }

        public AutoRestorePointResult Result { get; set; } = new(AutoRestorePointOutcome.SkippedUnavailable, null);

        public Task<AutoRestorePointResult> EnsureAsync(string reason, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeService : IWindowsServicesService
    {
        public IReadOnlyList<WindowsServiceEntry> Entries { get; set; } = [];

        public List<string> Calls { get; } = [];

        public ServiceChangeOutcome Next { get; set; } = ServiceChangeOutcome.Of(ServiceChangeResult.Changed);

        public Action? OnChange { get; set; }

        public bool ThrowOnList { get; set; }

        public Task<IReadOnlyList<WindowsServiceEntry>> ListAsync(CancellationToken cancellationToken) =>
            ThrowOnList ? throw new InvalidOperationException("boom") : Task.FromResult(Entries);

        public Task<ServiceChangeOutcome> StartAsync(string name, CancellationToken cancellationToken) => Record($"start {name}");

        public Task<ServiceChangeOutcome> StopAsync(string name, CancellationToken cancellationToken) => Record($"stop {name}");

        public Task<ServiceChangeOutcome> RestartAsync(string name, CancellationToken cancellationToken) => Record($"restart {name}");

        public Task<ServiceChangeOutcome> SetStartTypeAsync(string name, ServiceStartType startType, CancellationToken cancellationToken) =>
            Record($"type {name} {startType}");

        private Task<ServiceChangeOutcome> Record(string call)
        {
            Calls.Add(call);
            if (Next.Result == ServiceChangeResult.Changed)
            {
                OnChange?.Invoke();
            }

            return Task.FromResult(Next);
        }
    }

    private sealed class FakeShell : IShellService
    {
        public bool IsElevated { get; set; }

        public IRelayCommand RestartElevatedCommand { get; } = new RelayCommand(() => { });
    }

    private sealed class FakeConfirm : IConfirmationDialog
    {
        public bool Answer { get; set; } = true;

        public List<string> Messages { get; } = [];

        public bool Confirm(string title, string message)
        {
            Messages.Add(message);
            return Answer;
        }
    }
}
