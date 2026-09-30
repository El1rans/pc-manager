using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.Dashboard;
using Porchlight.App.Shell;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.App.Tests.Features.Dashboard;

public sealed class DashboardViewModelTests
{
    private static DashboardViewModel Create(IPageNavigator navigator) =>
        new(
            new FakePerformanceSampler(),
            new FakeSystemInfoProvider(),
            new FakeProcessMonitor(),
            new FakeDriveMonitor(),
            new FakeRestartDetector(),
            navigator,
            NullLogger<DashboardViewModel>.Instance);

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var viewModel = Create(new PageNavigator());

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void FreeUpSpaceCommand_OpensTheFreeUpSpacePage()
    {
        var navigator = new PageNavigator();
        Type? requested = null;
        navigator.NavigationRequested += type => requested = type;
        var viewModel = Create(navigator);

        viewModel.FreeUpSpaceCommand.Execute(null);
        viewModel.Dispose();

        Assert.Equal(typeof(CleanupViewModel), requested);
    }

    [Fact]
    public void DriveRow_FlagsALowSpaceDrive_SoTheButtonShows()
    {
        var low = new DriveRowViewModel(new DriveSnapshot(@"C:\", "Windows", "NTFS", 100L << 30, 5L << 30, IsLow: true));
        var fine = new DriveRowViewModel(new DriveSnapshot(@"D:\", null, "NTFS", 100L << 30, 90L << 30, IsLow: false));

        Assert.True(low.IsLow);
        Assert.False(fine.IsLow);
    }

    [Fact]
    public async Task FirstTick_ShowsCpuAndMemoryImmediately_WhileCountersAreStillWarmingUp()
    {
        var sampler = new WarmingUpSampler();
        using var viewModel = new DashboardViewModel(
            sampler,
            new FakeSystemInfoProvider(),
            new FakeProcessMonitor(),
            new FakeDriveMonitor(),
            new FakeRestartDetector(),
            new PageNavigator(),
            NullLogger<DashboardViewModel>.Instance);

        // Well under the 1-second sample interval: the first tick must not wait for it.
        var deadline = DateTime.UtcNow.AddMilliseconds(700);
        while (viewModel.MemoryTile.ValueText == "–" && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal("50%", viewModel.MemoryTile.ValueText);
        Assert.Equal("25%", viewModel.CpuTile.ValueText);
        Assert.Equal("–", viewModel.GpuTile.ValueText);
        Assert.Equal("–", viewModel.DiskTile.ValueText);
        Assert.Equal(0, sampler.BlockingSampleCalls);
    }

    /// <summary>Counters never finish warming up; only <see cref="SampleWithoutWaiting"/> is
    /// expected to be called.</summary>
    private sealed class WarmingUpSampler : IPerformanceSampler
    {
        public int BlockingSampleCalls { get; private set; }

        public bool IsWarmedUp => false;

        public void WarmUp()
        {
        }

        public PerformanceSnapshot Sample()
        {
            BlockingSampleCalls++;
            return SampleWithoutWaiting();
        }

        public PerformanceSnapshot SampleWithoutWaiting() =>
            new(25, 50, 100, null, null, null, null, null, null, 0, 0);

        public void Dispose()
        {
        }
    }

    private sealed class FakePerformanceSampler : IPerformanceSampler
    {
        public PerformanceSnapshot Sample() => new(null, null, null, null, null, null, null, null, null, 0, 0);

        public void Dispose()
        {
        }
    }

    private sealed class FakeSystemInfoProvider : ISystemInfoProvider
    {
        public Task<SystemInfo> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SystemInfo("PC", "Windows", "1", "Maker", "Model", "CPU", 1, 1, [], 0, null));
    }

    private sealed class FakeProcessMonitor : IProcessMonitor
    {
        public IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count) => [];
    }

    private sealed class FakeDriveMonitor : IDriveMonitor
    {
        public IReadOnlyList<DriveSnapshot> GetDrives() => [];
    }

    private sealed class FakeRestartDetector : IRestartDetector
    {
        public bool IsRestartPending() => false;
    }
}
