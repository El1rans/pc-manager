using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Dashboard;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.App.Tests.Features.Dashboard;

public sealed class DashboardViewModelTests
{
    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var viewModel = new DashboardViewModel(
            new FakePerformanceSampler(),
            new FakeSystemInfoProvider(),
            new FakeProcessMonitor(),
            new FakeDriveMonitor(),
            new FakeRestartDetector(),
            NullLogger<DashboardViewModel>.Instance);

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
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
