using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Alerts;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class AlertHostedServiceTests : IDisposable
{
    private const long Gb = 1024L * 1024 * 1024;

    private readonly TimerCountingTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeInputProvider _inputs = new();
    private readonly FakeTrayIcon _tray = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly AlertHostedService _service;

    public AlertHostedServiceTests()
    {
        _service = new AlertHostedService(
            _inputs,
            new AlertEvaluator(_time, new FakeAlertStateStore()),
            new AlertNotifier(_tray, new FakeShellWindowService()),
            _settings,
            NullLogger<AlertHostedService>.Instance,
            _time);
    }

    public void Dispose() => _service.Dispose();

    private static DriveSnapshot LowDrive(string name) => new(name, null, "NTFS", 500 * Gb, 2 * Gb, true);

    /// <summary>Starts the loop and waits until it has armed its initial-delay timer, so the first
    /// <c>Advance</c> is not lost.</summary>
    private async Task StartAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await BackgroundLoop.WaitUntilAsync(() => _time.TimersCreated >= 1);
    }

    /// <summary>Runs through the initial delay and waits for the first round. The loop creates its
    /// periodic timer before that round, so the next advance always counts towards the next tick.</summary>
    private async Task RunFirstRoundAsync()
    {
        await StartAsync();
        _time.Advance(TimeSpan.FromSeconds(30));
        await BackgroundLoop.WaitUntilAsync(() => _inputs.Calls == 1);
    }

    [Fact]
    public async Task FirstCheck_RunsAfterThe30SecondInitialDelay_NotBefore()
    {
        await StartAsync();

        _time.Advance(TimeSpan.FromSeconds(29));
        await BackgroundLoop.SettleAsync();
        Assert.Equal(0, _inputs.Calls);

        _time.Advance(TimeSpan.FromSeconds(1));
        await BackgroundLoop.WaitUntilAsync(() => _inputs.Calls == 1);
        Assert.Equal(1, _inputs.Calls);
    }

    [Fact]
    public async Task Checks_RepeatEveryMinute()
    {
        await RunFirstRoundAsync();

        _time.Advance(TimeSpan.FromSeconds(59));
        await BackgroundLoop.SettleAsync();
        Assert.Equal(1, _inputs.Calls);

        _time.Advance(TimeSpan.FromSeconds(1));
        await BackgroundLoop.WaitUntilAsync(() => _inputs.Calls == 2);
        Assert.Equal(2, _inputs.Calls);
    }

    [Fact]
    public async Task AlertingInputs_ShowABalloon()
    {
        _inputs.Drives = [LowDrive("C:\\")];

        await RunFirstRoundAsync();
        await BackgroundLoop.WaitUntilAsync(() => _tray.Balloons.Count == 1);

        var balloon = Assert.Single(_tray.Balloons);
        Assert.Contains("C:", balloon, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TurnedOffAlertType_ShowsNothing()
    {
        _settings.Current.Notifications.AlertLowDisk = false;
        _inputs.Drives = [LowDrive("C:\\")];

        await RunFirstRoundAsync();
        await BackgroundLoop.SettleAsync();

        Assert.Equal(1, _inputs.Calls);
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public async Task SeveralAlertsInOneRound_AreSpacedEightSecondsApart()
    {
        _inputs.Drives = [LowDrive("C:\\"), LowDrive("D:\\")];

        // Timers: initial delay, periodic timer, then the spacing delay before the second balloon.
        await RunFirstRoundAsync();
        await BackgroundLoop.WaitUntilAsync(() => _tray.Balloons.Count == 1 && _time.TimersCreated >= 3);
        Assert.Single(_tray.Balloons);

        _time.Advance(TimeSpan.FromSeconds(7));
        await BackgroundLoop.SettleAsync();
        Assert.Single(_tray.Balloons);

        _time.Advance(TimeSpan.FromSeconds(1));
        await BackgroundLoop.WaitUntilAsync(() => _tray.Balloons.Count == 2);
        Assert.Equal(2, _tray.Balloons.Count);
    }

    [Fact]
    public async Task FailingRound_DoesNotEndTheLoop()
    {
        _inputs.FailNextCall = true;
        _inputs.Drives = [LowDrive("C:\\")];

        await RunFirstRoundAsync();
        await BackgroundLoop.SettleAsync();
        Assert.Empty(_tray.Balloons);

        _time.Advance(TimeSpan.FromMinutes(1));
        await BackgroundLoop.WaitUntilAsync(() => _tray.Balloons.Count == 1);
        Assert.Single(_tray.Balloons);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoop()
    {
        await RunFirstRoundAsync();

        // StopAsync waits for the loop to finish, so nothing can run after this point.
        await _service.StopAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, _inputs.Calls);
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNothing()
    {
        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(0, _inputs.Calls);
    }

    private sealed class FakeInputProvider : IAlertInputProvider
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public IReadOnlyList<DriveSnapshot> Drives { get; set; } = [];

        public bool FailNextCall { get; set; }

        public AlertInputs GetInputs()
        {
            Interlocked.Increment(ref _calls);
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new InvalidOperationException("boom");
            }

            return new AlertInputs(Drives, null, null, false);
        }
    }
}
