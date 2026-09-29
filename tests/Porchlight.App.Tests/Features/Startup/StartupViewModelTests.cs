using Porchlight.App.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Startup;
using Porchlight.Core.Elevation;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.App.Tests.Features.Startup;

public sealed class StartupViewModelTests
{
    private readonly FakeStartupService _service = new();
    private readonly FakeElevation _elevation = new();

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

        public StartupChangeResult Result { get; set; } = StartupChangeResult.Changed;

        public List<(string Id, bool Enabled)> Changes { get; } = [];

        public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Entries);

        public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken)
        {
            Changes.Add((entryId, enabled));
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeElevation : IElevationService
    {
        public bool IsElevated { get; set; }

        public bool RestartElevated() => true;
    }
}
