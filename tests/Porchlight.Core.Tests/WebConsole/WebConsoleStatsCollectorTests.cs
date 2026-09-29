using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Tests.Hardware;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public sealed class WebConsoleStatsCollectorTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly CountingSampler _sampler = new();
    private readonly CountingProcessMonitor _processes = new();
    private readonly CountingDriveMonitor _drives = new();
    private readonly WebConsoleStatsCollector _collector;

    public WebConsoleStatsCollectorTests()
    {
        _collector = new WebConsoleStatsCollector(
            new FixedSystemInfoProvider(),
            _sampler,
            _processes,
            _drives,
            new FixedRestartDetector(),
            new FakeHardwareService(),
            new FakeWebConsoleSettingsStore(),
            NullLogger<WebConsoleStatsCollector>.Instance,
            _time,
            TimeSpan.Zero);
    }

    public void Dispose() => _collector.Dispose();

    [Fact]
    public async Task First_request_re_primes_rate_sources_before_the_real_sample()
    {
        var stats = await _collector.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _sampler.Calls);
        Assert.Equal(2, _processes.Calls);
        Assert.Equal("DEMO-PC", stats.SystemInfo?.ComputerName);
        Assert.True(stats.IsRestartPending);
        Assert.Single(stats.Drives);
    }

    [Fact]
    public async Task Requests_within_the_minimum_interval_share_one_sample()
    {
        var first = await _collector.GetAsync(TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMilliseconds(500));

        var second = await _collector.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(2, _sampler.Calls);
    }

    [Fact]
    public async Task Regular_polling_samples_once_per_request_and_reuses_slow_sources()
    {
        await _collector.GetAsync(TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(2));

        await _collector.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, _sampler.Calls);
        Assert.Equal(1, _drives.Calls);
    }

    [Fact]
    public async Task A_failing_source_is_left_out_rather_than_failing_the_request()
    {
        _sampler.Throw = true;

        var stats = await _collector.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(stats.Performance);
        Assert.Single(stats.TopProcesses);
    }

    private sealed class CountingSampler : IPerformanceSampler
    {
        public int Calls { get; private set; }

        public bool Throw { get; set; }

        public PerformanceSnapshot Sample()
        {
            Calls++;
            if (Throw)
            {
                throw new InvalidOperationException("counter broken");
            }

            return FakeStatsSource.Sample.Performance!;
        }

        public void Dispose()
        {
        }
    }

    private sealed class CountingProcessMonitor : IProcessMonitor
    {
        public int Calls { get; private set; }

        public IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count)
        {
            Calls++;
            return FakeStatsSource.Sample.TopProcesses;
        }
    }

    private sealed class CountingDriveMonitor : IDriveMonitor
    {
        public int Calls { get; private set; }

        public IReadOnlyList<DriveSnapshot> GetDrives()
        {
            Calls++;
            return FakeStatsSource.Sample.Drives;
        }
    }

    private sealed class FixedSystemInfoProvider : ISystemInfoProvider
    {
        public Task<SystemInfo> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(FakeStatsSource.Sample.SystemInfo!);
    }

    private sealed class FixedRestartDetector : IRestartDetector
    {
        public bool IsRestartPending() => true;
    }
}
