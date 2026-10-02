using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Features.Updates;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Alerts;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class ScheduledUpdateCheckHostedServiceTests : IDisposable
{
    private readonly TimerCountingTimeProvider _time = new(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeUpdateChecker _checker = new();
    private readonly FakeTrayIcon _tray = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly ScheduledUpdateCheckHostedService _service;

    public ScheduledUpdateCheckHostedServiceTests()
    {
        _service = new ScheduledUpdateCheckHostedService(
            _checker,
            new AlertEvaluator(_time, new FakeAlertStateStore()),
            new AlertNotifier(_tray, new FakeShellWindowService()),
            _settings,
            NullLogger<ScheduledUpdateCheckHostedService>.Instance,
            _time);
    }

    public void Dispose() => _service.Dispose();

    private NotificationSettings Notifications => _settings.Current.Notifications;

    /// <summary>Starts the loop and waits until it has armed its initial-delay timer, so the first
    /// <c>Advance</c> is not lost.</summary>
    private async Task StartAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await BackgroundLoop.WaitUntilAsync(() => _time.TimersCreated >= 1);
    }

    /// <summary>Runs through the five-minute initial delay to the first schedule decision.
    /// <paramref name="decided"/> says how to tell that decision has finished; when it leaves no
    /// trace (nothing was due), the test only checks for the absence of effects, so a short settle
    /// after the periodic timer exists is enough.</summary>
    private async Task ReachFirstDecisionAsync(Func<bool>? decided = null)
    {
        await StartAsync();
        _time.Advance(TimeSpan.FromMinutes(5));
        if (decided is null)
        {
            await BackgroundLoop.WaitUntilAsync(() => _time.TimersCreated >= 2);
            await BackgroundLoop.SettleAsync();
        }
        else
        {
            await BackgroundLoop.WaitUntilAsync(decided);
        }
    }

    [Fact]
    public async Task FirstDecision_WaitsFiveMinutes()
    {
        await StartAsync();

        _time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        await BackgroundLoop.SettleAsync();
        Assert.Equal(0, _checker.Calls);

        _time.Advance(TimeSpan.FromSeconds(1));
        await BackgroundLoop.WaitUntilAsync(() => _checker.Calls == 1);
        Assert.Equal(1, _checker.Calls);
    }

    [Fact]
    public async Task CheckNotYetDue_DoesNotCheck()
    {
        Notifications.LastScheduledUpdateCheckUtc = _time.GetUtcNow() - TimeSpan.FromHours(1);

        await ReachFirstDecisionAsync();

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task ScheduleNever_DoesNotCheck()
    {
        Notifications.UpdateCheckSchedule = UpdateCheckSchedule.Never;

        await ReachFirstDecisionAsync();

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task SuccessfulCheck_RecordsWhenItRan_AndShowsTheUpdatesAlert()
    {
        _checker.Result = UpdateCheckResult.Succeeded(3);

        await ReachFirstDecisionAsync(() => _tray.Balloons.Count == 1);

        Assert.Equal(_time.GetUtcNow(), Notifications.LastScheduledUpdateCheckUtc);
        Assert.Equal("3 app updates are ready", Assert.Single(_tray.Balloons));
    }

    [Fact]
    public async Task SuccessfulCheckWithNothingToUpdate_RecordsItButShowsNothing()
    {
        _checker.Result = UpdateCheckResult.Succeeded(0);

        await ReachFirstDecisionAsync(() => Notifications.LastScheduledUpdateCheckUtc is not null);
        await BackgroundLoop.SettleAsync();

        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public async Task UpdatesAlertTurnedOff_RecordsTheCheckButShowsNothing()
    {
        Notifications.AlertUpdates = false;
        _checker.Result = UpdateCheckResult.Succeeded(2);

        await ReachFirstDecisionAsync(() => Notifications.LastScheduledUpdateCheckUtc is not null);
        await BackgroundLoop.SettleAsync();

        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);
    }

    [Theory]
    [InlineData(UpdateCheckStatus.Failed)]
    [InlineData(UpdateCheckStatus.Skipped)]
    public async Task CheckThatDidNotComplete_IsNotRecorded_AndRetriedTheNextHour(UpdateCheckStatus status)
    {
        _checker.Result = new UpdateCheckResult(status, 0);
        await ReachFirstDecisionAsync(() => _checker.Calls == 1);
        await BackgroundLoop.SettleAsync();

        Assert.Null(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);

        _time.Advance(TimeSpan.FromHours(1));
        await BackgroundLoop.WaitUntilAsync(() => _checker.Calls == 2);
        Assert.Equal(2, _checker.Calls);
    }

    [Fact]
    public async Task CheckerThrowing_DoesNotEndTheLoop()
    {
        _checker.FailNextCall = true;
        await ReachFirstDecisionAsync(() => _checker.Calls == 1);
        Assert.Null(Notifications.LastScheduledUpdateCheckUtc);

        _time.Advance(TimeSpan.FromHours(1));
        await BackgroundLoop.WaitUntilAsync(() => Notifications.LastScheduledUpdateCheckUtc is not null);

        Assert.Equal(2, _checker.Calls);
        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoop()
    {
        _checker.Result = UpdateCheckResult.Failed;
        await ReachFirstDecisionAsync(() => _checker.Calls == 1);

        // StopAsync waits for the loop to finish, so nothing can run after this point.
        await _service.StopAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(1, _checker.Calls);
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNothing()
    {
        await _service.StopAsync(CancellationToken.None);

        Assert.Equal(0, _checker.Calls);
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public UpdateCheckResult Result { get; set; } = UpdateCheckResult.Succeeded(1);

        public bool FailNextCall { get; set; }

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(Result);
        }
    }
}
