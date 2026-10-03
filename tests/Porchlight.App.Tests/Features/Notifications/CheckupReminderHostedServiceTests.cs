using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Checkup;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class CheckupReminderHostedServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)); // a Sunday
    private readonly FakeTrayIcon _tray = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly CheckupReminderHostedService _service;

    public CheckupReminderHostedServiceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _service = new CheckupReminderHostedService(
            new CheckupReminderScheduler(_time),
            _settings,
            _tray,
            new FakeShellWindowService(),
            NullLogger<CheckupReminderHostedService>.Instance,
            _time);
    }

    public void Dispose() => _service.Dispose();

    private void TurnOn() => _settings.Current.CheckupReminder.Enabled = true;

    [Fact]
    public void Off_ShowsNothing()
    {
        Assert.False(_service.CheckOnce());
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public void Due_ShowsOneBalloon_AndRecordsIt_ThenStaysQuiet()
    {
        TurnOn();

        Assert.True(_service.CheckOnce());
        Assert.Equal([CheckupReminderHostedService.BalloonTitle], _tray.Balloons);
        Assert.Equal(_time.GetUtcNow(), _settings.Current.CheckupReminder.LastReminderShownUtc);

        // The next periodic checks the same day (and for the following week) show nothing more.
        Assert.False(_service.CheckOnce());
        _time.Advance(TimeSpan.FromDays(6));
        Assert.False(_service.CheckOnce());
        Assert.Single(_tray.Balloons);

        _time.Advance(TimeSpan.FromDays(1));
        Assert.True(_service.CheckOnce());
        Assert.Equal(2, _tray.Balloons.Count);
    }

    [Fact]
    public void ReportMadeRecently_ShowsNothing()
    {
        TurnOn();
        _settings.Current.CheckupReminder.LastReportCreatedUtc = _time.GetUtcNow().AddDays(-1);

        Assert.False(_service.CheckOnce());
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public async Task Loop_FirstChecksAfterTheInitialDelay()
    {
        TurnOn();
        var time = new TimerCountingTimeProvider(_time.GetUtcNow());
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        using var service = new CheckupReminderHostedService(
            new CheckupReminderScheduler(time), _settings, _tray, new FakeShellWindowService(),
            NullLogger<CheckupReminderHostedService>.Instance, time);

        await service.StartAsync(CancellationToken.None);
        await BackgroundLoop.WaitUntilAsync(() => time.TimersCreated >= 1);
        time.Advance(CheckupReminderHostedService.InitialDelay - TimeSpan.FromSeconds(1));
        await BackgroundLoop.SettleAsync();
        Assert.Empty(_tray.Balloons);

        time.Advance(TimeSpan.FromSeconds(1));
        await BackgroundLoop.WaitUntilAsync(() => _tray.Balloons.Count == 1);
        Assert.Single(_tray.Balloons);

        await service.StopAsync(CancellationToken.None);
    }
}
