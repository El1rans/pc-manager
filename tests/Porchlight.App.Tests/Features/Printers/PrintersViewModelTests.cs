using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.Printers;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Printing;
using Xunit;

namespace Porchlight.App.Tests.Features.Printers;

public sealed class PrintersViewModelTests
{
    private readonly FakeService _service = new();
    private readonly FakeActions _actions = new();
    private readonly FakeShell _shell = new() { IsElevated = true };
    private readonly FakeConfirm _confirm = new();
    private readonly FakeLauncher _launcher = new();

    private PrintersViewModel Create() => new(
        _service, _actions, new PrinterFixFlow(_actions), _shell, _confirm, _launcher, NullLogger<PrintersViewModel>.Instance);

    private static PrinterEntry Entry(
        string name, bool isDefault = false, PrinterStatusKind status = PrinterStatusKind.Ready, int jobs = 0,
        bool workOffline = false, bool isVirtual = false) =>
        new(name, isDefault, workOffline, status, "USB001", false, jobs, isVirtual);

    private async Task<PrintersViewModel> LoadAsync(params PrinterEntry[] entries)
    {
        _service.Entries = entries;
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [Fact]
    public async Task Load_SplitsRealAndVirtualPrintersAndNamesTheDefault()
    {
        var vm = await LoadAsync(
            Entry("Brother", isDefault: true), Entry("HP"), Entry("Microsoft Print to PDF", isVirtual: true));

        Assert.Equal("Printers", vm.Title);
        Assert.Equal(PageCategory.Hardware, vm.Category);
        Assert.Equal(["Brother", "HP"], vm.Printers.Select(p => p.Name));
        Assert.Equal(["Microsoft Print to PDF"], vm.OtherPrinters.Select(p => p.Name));
        Assert.True(vm.HasOtherPrinters);
        Assert.Equal("Default printer: Brother", vm.DefaultPrinterText);
    }

    [Fact]
    public async Task Row_ShowsStatusAsIconAndTextAndJobsInWords()
    {
        var vm = await LoadAsync(Entry("HP", status: PrinterStatusKind.PaperJam, jobs: 2));

        var row = vm.Printers.Single();
        Assert.Equal("Paper jam", row.StatusText);
        Assert.NotEmpty(row.StatusGlyph);
        Assert.Equal("2 documents waiting to print", row.JobsText);
        Assert.True(row.CanClearJobs);
    }

    [Fact]
    public async Task EmptyList_ShowsEmptyState()
    {
        var vm = await LoadAsync();

        Assert.True(vm.ShowEmptyState);
        Assert.Equal("No default printer is set.", vm.DefaultPrinterText);
    }

    [Fact]
    public async Task LoadFailure_ShowsMessage()
    {
        _service.Throw = true;
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Couldn't read the printers. Try Refresh.", vm.ErrorMessage);
        Assert.False(vm.ShowEmptyState);
    }

    [Fact]
    public async Task MakeDefault_CallsActionAndRefreshes()
    {
        var vm = await LoadAsync(Entry("Brother", isDefault: true), Entry("HP"));
        var hp = vm.Printers.Single(p => p.Name == "HP");
        Assert.True(hp.CanMakeDefault);

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.MakeDefaultCommand).ExecuteAsync(hp);

        Assert.Equal(["default:HP"], _actions.Calls);
        Assert.Equal("HP is now your default printer.", vm.Message);
        Assert.Equal(2, _service.ListCount);
    }

    [Fact]
    public async Task ClearJobs_AsksFirstAndDoesNothingWhenDeclined()
    {
        var vm = await LoadAsync(Entry("HP", jobs: 3));
        _confirm.Answer = false;

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.ClearJobsCommand).ExecuteAsync(vm.Printers.Single());

        Assert.Equal(1, _confirm.AskCount);
        Assert.Empty(_actions.Calls);
    }

    [Fact]
    public async Task ClearJobs_ConfirmedClearsThatPrinter()
    {
        var vm = await LoadAsync(Entry("HP", jobs: 3));

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.ClearJobsCommand).ExecuteAsync(vm.Printers.Single());

        Assert.Equal(["clear:HP"], _actions.Calls);
        Assert.Equal("Cleared the waiting jobs for HP.", vm.Message);
    }

    [Fact]
    public async Task NeedsAdmin_ShowsAdminMessage()
    {
        var vm = await LoadAsync(Entry("HP", jobs: 1));
        _actions.ClearResult = PrinterActionOutcome.Of(PrinterActionResult.NeedsAdmin);

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.ClearJobsCommand).ExecuteAsync(vm.Printers.Single());

        Assert.Contains("administrator", vm.ErrorMessage);
    }

    [Fact]
    public async Task OfflinePrinter_ShowsHintAndUseOnlineWorks()
    {
        var vm = await LoadAsync(Entry("HP", status: PrinterStatusKind.Offline, workOffline: true));
        var row = vm.Printers.Single();
        Assert.True(row.IsOffline);
        Assert.True(row.CanUseOnline);
        Assert.Contains("Use printer offline", row.OfflineHint);

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.UseOnlineCommand).ExecuteAsync(row);

        Assert.Equal(["online:HP"], _actions.Calls);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task UseOnline_WhenWindowsRefuses_OpensPrinterSettings()
    {
        var vm = await LoadAsync(Entry("HP", status: PrinterStatusKind.Offline, workOffline: true));
        _actions.OnlineResult = PrinterActionOutcome.Of(PrinterActionResult.NotSupported);

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.UseOnlineCommand).ExecuteAsync(vm.Printers.Single());

        Assert.Equal(["ms-settings:printers"], _launcher.Opened);
        Assert.Contains("Use printer offline", vm.Message);
    }

    [Fact]
    public async Task OpenSettings_LaunchesPrinterSettings()
    {
        var vm = await LoadAsync(Entry("HP"));

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(["ms-settings:printers"], _launcher.Opened);
    }

    [Fact]
    public async Task TestPage_SendsToThatPrinter()
    {
        var vm = await LoadAsync(Entry("HP"));

        await ((IAsyncRelayCommand<PrinterRowViewModel>)vm.PrintTestPageCommand).ExecuteAsync(vm.Printers.Single());

        Assert.Equal(["test:HP"], _actions.Calls);
        Assert.Equal("Sent a test page to HP.", vm.Message);
    }

    [Fact]
    public async Task RestartService_NeedsElevationAndAsksFirst()
    {
        _shell.IsElevated = false;
        var vm = await LoadAsync(Entry("HP"));
        Assert.False(vm.CanRestartService);
        Assert.True(vm.ShowAdminBanner);

        await vm.RestartServiceCommand.ExecuteAsync(null);
        Assert.Empty(_actions.Calls);

        _shell.IsElevated = true;
        var elevated = await LoadAsync(Entry("HP", jobs: 2));
        _confirm.Answer = false;
        await elevated.RestartServiceCommand.ExecuteAsync(null);
        Assert.Empty(_actions.Calls);

        _confirm.Answer = true;
        await elevated.RestartServiceCommand.ExecuteAsync(null);
        Assert.Equal(["restart"], _actions.Calls);
        Assert.True(_actions.LastClearSpoolFiles);
        Assert.False(elevated.ShowAdminBanner);
    }

    [Fact]
    public async Task FixMyPrinter_RunsTheFlowOnTheDefaultPrinterAndListsEachStep()
    {
        var vm = await LoadAsync(Entry("Brother", isDefault: true, jobs: 2), Entry("HP"));
        _actions.ClearResult = new PrinterActionOutcome(PrinterActionResult.Done, 2);

        await vm.FixMyPrinterCommand.ExecuteAsync(null);

        Assert.Equal(["state", "clear:Brother", "restart"], _actions.Calls);
        Assert.Equal(3, vm.FixSteps.Count);
        Assert.All(vm.FixSteps, s => Assert.NotEmpty(s.Glyph));
        Assert.Equal("Done. Try printing again.", vm.FixSummary);
        Assert.True(vm.HasFixSteps);
        Assert.False(vm.IsBusy);
        Assert.Equal(2, _service.ListCount);
    }

    [Fact]
    public async Task FixMyPrinter_DeclinedDoesNothing()
    {
        var vm = await LoadAsync(Entry("Brother", isDefault: true));
        _confirm.Answer = false;

        await vm.FixMyPrinterCommand.ExecuteAsync(null);

        Assert.Empty(_actions.Calls);
        Assert.Empty(vm.FixSteps);
    }

    private sealed class FakeService : IPrinterService
    {
        public IReadOnlyList<PrinterEntry> Entries { get; set; } = [];

        public bool Throw { get; set; }

        public int ListCount { get; private set; }

        public Task<IReadOnlyList<PrinterEntry>> ListAsync(CancellationToken cancellationToken)
        {
            ListCount++;
            return Throw ? throw new InvalidOperationException("boom") : Task.FromResult(Entries);
        }
    }

    private sealed class FakeActions : IPrinterActions
    {
        public PrinterActionOutcome ClearResult { get; set; } = new(PrinterActionResult.Done, 0);

        public PrinterActionOutcome OnlineResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

        public List<string> Calls { get; } = [];

        public bool? LastClearSpoolFiles { get; private set; }

        private static Task<PrinterActionOutcome> Done() => Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.Done));

        public Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken)
        {
            Calls.Add($"default:{printerName}");
            return Done();
        }

        public Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken)
        {
            Calls.Add($"online:{printerName}");
            return Task.FromResult(OnlineResult);
        }

        public Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken)
        {
            Calls.Add($"test:{printerName}");
            return Done();
        }

        public Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken)
        {
            Calls.Add($"clear:{printerName}");
            return Task.FromResult(ClearResult);
        }

        public Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken)
        {
            Calls.Add("state");
            return Task.FromResult(SpoolerState.Running);
        }

        public Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken)
        {
            Calls.Add("start");
            return Done();
        }

        public Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken)
        {
            Calls.Add("restart");
            LastClearSpoolFiles = clearSpoolFiles;
            return Done();
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

        public int AskCount { get; private set; }

        public bool Confirm(string title, string message)
        {
            AskCount++;
            return Answer;
        }
    }

    private sealed class FakeLauncher : IUrlLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string url) => Opened.Add(url);
    }
}
