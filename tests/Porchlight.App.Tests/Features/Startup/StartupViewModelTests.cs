using Porchlight.App.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Startup;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Elevation;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.App.Tests.Features.Startup;

public sealed class StartupViewModelTests
{
    private readonly FakeStartupService _service = new();
    private readonly FakeElevationService _elevation = new();

    private StartupViewModel Create() => new(_service, _elevation, NullLogger<StartupViewModel>.Instance);

    private static StartupEntry Entry(StartupSource source, string name, bool enabled = true) =>
        new($"{source}|{name}", source, name, name, null, null, "hint", false, enabled);

    [Fact]
    public async Task Load_BuildsSummaryAndOrder()
    {
        _service.Entries = [Entry(StartupSource.CurrentUserRun, "A"), Entry(StartupSource.CurrentUserRun, "B", enabled: false)];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.Order);
        Assert.Equal(PageCategory.TuneUp, vm.Category);
        Assert.Equal("Startup apps", vm.Title);
        Assert.Equal("2 apps start with Windows: 1 on, 1 off.", vm.Summary);
        Assert.False(vm.ShowAdminBanner);
    }

    private static StartupEntry WithImpact(string name, StartupImpact impact, bool enabled = true) =>
        Entry(StartupSource.CurrentUserRun, name, enabled) with { Impact = impact };

    [Fact]
    public async Task Load_ImpactChipShowsTextForEachRating()
    {
        _service.Entries =
        [
            WithImpact("H", StartupImpact.High), WithImpact("M", StartupImpact.Medium),
            WithImpact("L", StartupImpact.Low), WithImpact("N", StartupImpact.NotMeasured),
        ];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["High impact", "Medium impact", "Low impact", "Not measured"], vm.Items.Select(i => i.ImpactText));
        Assert.All(vm.Items, i => Assert.False(string.IsNullOrEmpty(i.ImpactGlyph)));
    }

    [Fact]
    public async Task Summary_MentionsHighImpactItemsThatAreOn()
    {
        _service.Entries =
        [
            WithImpact("A", StartupImpact.High), WithImpact("B", StartupImpact.High),
            WithImpact("C", StartupImpact.High, enabled: false), WithImpact("D", StartupImpact.Low),
        ];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("4 apps start with Windows: 3 on, 1 off, 2 have high impact.", vm.Summary);
    }

    [Fact]
    public async Task Summary_SingleHighImpactItem_UsesSingularVerb()
    {
        _service.Entries = [WithImpact("A", StartupImpact.High)];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1 app starts with Windows: 1 on, 0 off, 1 has high impact.", vm.Summary);
    }

    [Fact]
    public async Task SortByImpact_OrdersHighestFirstAndKeepsNameOrderWithinARating()
    {
        _service.Entries =
        [
            WithImpact("A", StartupImpact.Low), WithImpact("B", StartupImpact.High),
            WithImpact("C", StartupImpact.NotMeasured), WithImpact("D", StartupImpact.High),
            WithImpact("E", StartupImpact.Medium),
        ];
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["A", "B", "C", "D", "E"], vm.Items.Select(i => i.Name));

        vm.SortByImpact = true;

        Assert.Equal(["B", "D", "E", "A", "C"], vm.Items.Select(i => i.Name));

        vm.SortByImpact = false;

        Assert.Equal(["A", "B", "C", "D", "E"], vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task ImpactHint_OnlyWhenTraceWasDeniedAndNotElevated()
    {
        _service.Entries = [Entry(StartupSource.CurrentUserRun, "A")];
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.False(vm.ShowImpactHint);

        _service.ImpactNeedsAdmin = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.ShowImpactHint);

        _elevation.IsElevated = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.ShowImpactHint);
    }

    [Fact]
    public async Task Load_LogonTask_ShowsScheduledTaskSource()
    {
        _service.Entries = [Entry(StartupSource.LogonTask, @"\Vendor\Sync")];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Scheduled task", vm.Items[0].SourceLabel);
        Assert.True(vm.Items[0].CanChange);
    }

    [Fact]
    public async Task Load_MachineWideTaskWhenNotElevated_ShowsBannerAndBlocksChange()
    {
        _service.Entries = [Entry(StartupSource.LogonTask, @"\Vendor\Svc") with { IsMachineWide = true }];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.ShowAdminBanner);
        Assert.False(vm.Items[0].CanChange);
    }

    [Fact]
    public async Task Load_MachineEntryWhenNotElevated_ShowsBannerAndBlocksChange()
    {
        _service.Entries = [Entry(StartupSource.MachineRun, "M")];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.ShowAdminBanner);
        Assert.False(vm.Items[0].CanChange);
        Assert.True(vm.Items[0].ShowNeedsAdmin);
    }

    [Fact]
    public async Task Load_MachineEntryWhenElevated_AllowsChangeAndNoBanner()
    {
        _elevation.IsElevated = true;
        _service.Entries = [Entry(StartupSource.MachineRun, "M")];
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.False(vm.ShowAdminBanner);
        Assert.True(vm.Items[0].CanChange);
    }

    [Fact]
    public async Task Toggle_Success_FlipsStateAndUpdatesSummary()
    {
        _service.Entries = [Entry(StartupSource.CurrentUserRun, "A")];
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        await vm.ToggleCommand.ExecuteAsync(vm.Items[0]);

        Assert.False(vm.Items[0].IsEnabled);
        Assert.Equal("Off", vm.Items[0].StatusText);
        Assert.Equal("Turn on", vm.Items[0].ToggleLabel);
        Assert.Equal(("CurrentUserRun|A", false), _service.Changes.Single());
        Assert.Contains("0 on, 1 off", vm.Summary);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Toggle_Failure_KeepsStateAndShowsPlainError()
    {
        _service.Entries = [Entry(StartupSource.CurrentUserRun, "A")];
        _service.Result = StartupChangeResult.Failed;
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        await vm.ToggleCommand.ExecuteAsync(vm.Items[0]);

        Assert.True(vm.Items[0].IsEnabled);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_Empty_ShowsEmptyState()
    {
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.ShowEmptyState);
    }

    private sealed class FakeStartupService : IStartupService
    {
        public IReadOnlyList<StartupEntry> Entries { get; set; } = [];

        public bool ImpactNeedsAdmin { get; set; }

        public StartupChangeResult Result { get; set; } = StartupChangeResult.Changed;

        public List<(string Id, bool Enabled)> Changes { get; } = [];

        public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Entries);

        public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken)
        {
            Changes.Add((entryId, enabled));
            return Task.FromResult(Result);
        }
    }

}
