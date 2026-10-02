using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Features.Updates;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Alerts;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class ScheduledUpdateCheckHostedServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
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

    private static Task SettleAsync() => Task.Delay(100);

    private async Task StartAndSettleAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await SettleAsync();
    }

    private async Task AdvanceAsync(TimeSpan amount)
    {
        _time.Advance(amount);
        await SettleAsync();
    }

    /// <summary>Starts the service and runs through the five-minute initial delay to the first schedule decision.</summary>
    private async Task StartAndReachFirstDecisionAsync()
    {
        await StartAndSettleAsync();
        await AdvanceAsync(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task FirstDecision_WaitsFiveMinutes()
    {
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.Equal(0, _checker.Calls);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _checker.Calls);
    }

    [Fact]
    public async Task CheckNotYetDue_DoesNotCheck()
    {
        Notifications.LastScheduledUpdateCheckUtc = _time.GetUtcNow() - TimeSpan.FromHours(1);

        await StartAndReachFirstDecisionAsync();

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task ScheduleNever_DoesNotCheck()
    {
        Notifications.UpdateCheckSchedule = UpdateCheckSchedule.Never;

        await StartAndReachFirstDecisionAsync();

        Assert.Equal(0, _checker.Calls);
    }

    [Fact]
    public async Task SuccessfulCheck_RecordsWhenItRan_AndShowsTheUpdatesAlert()
    {
        _checker.Result = UpdateCheckResult.Succeeded(3);

        await StartAndReachFirstDecisionAsync();

        Assert.Equal(_time.GetUtcNow(), Notifications.LastScheduledUpdateCheckUtc);
        Assert.Equal("3 app updates are ready", Assert.Single(_tray.Balloons));
    }

    [Fact]
    public async Task SuccessfulCheckWithNothingToUpdate_RecordsItButShowsNothing()
    {
        _checker.Result = UpdateCheckResult.Succeeded(0);

        await StartAndReachFirstDecisionAsync();

        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public async Task UpdatesAlertTurnedOff_RecordsTheCheckButShowsNothing()
    {
        Notifications.AlertUpdates = false;
        _checker.Result = UpdateCheckResult.Succeeded(2);

        await StartAndReachFirstDecisionAsync();

        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);
    }

    [Theory]
    [InlineData(UpdateCheckStatus.Failed)]
    [InlineData(UpdateCheckStatus.Skipped)]
    public async Task CheckThatDidNotComplete_IsNotRecorded_AndRetriedTheNextHour(UpdateCheckStatus status)
    {
        _checker.Result = new UpdateCheckResult(status, 0);
        await StartAndReachFirstDecisionAsync();

        Assert.Null(Notifications.LastScheduledUpdateCheckUtc);
        Assert.Empty(_tray.Balloons);

        await AdvanceAsync(TimeSpan.FromHours(1));
        Assert.Equal(2, _checker.Calls);
    }

    [Fact]
    public async Task CheckerThrowing_DoesNotEndTheLoop()
    {
        _checker.FailNextCall = true;
        await StartAndReachFirstDecisionAsync();
        Assert.Null(Notifications.LastScheduledUpdateCheckUtc);

        await AdvanceAsync(TimeSpan.FromHours(1));

        Assert.Equal(2, _checker.Calls);
        Assert.NotNull(Notifications.LastScheduledUpdateCheckUtc);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoop()
    {
        _checker.Result = UpdateCheckResult.Failed;
        await StartAndReachFirstDecisionAsync();

        await _service.StopAsync(CancellationToken.None);
        await AdvanceAsync(TimeSpan.FromHours(3));

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
        public int Calls { get; private set; }

        public UpdateCheckResult Result { get; set; } = UpdateCheckResult.Succeeded(1);

        public bool FailNextCall { get; set; }

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(Result);
        }
    }
}
