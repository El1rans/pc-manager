using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Alerts;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class AlertHostedServiceTests : IDisposable
{
    private const long Gb = 1024L * 1024 * 1024;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
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

    private static Task SettleAsync() => Task.Delay(100);

    /// <summary>Starts the loop and lets it reach its first (initial-delay) timer before time moves.</summary>
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

    [Fact]
    public async Task FirstCheck_RunsAfterThe30SecondInitialDelay_NotBefore()
    {
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(29));
        Assert.Equal(0, _inputs.Calls);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _inputs.Calls);
    }

    [Fact]
    public async Task Checks_RepeatEveryMinute()
    {
        await StartAndSettleAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        await AdvanceAsync(TimeSpan.FromSeconds(59));
        Assert.Equal(1, _inputs.Calls);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(2, _inputs.Calls);
    }

    [Fact]
    public async Task AlertingInputs_ShowABalloon()
    {
        _inputs.Drives = [LowDrive("C:\\")];
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var balloon = Assert.Single(_tray.Balloons);
        Assert.Contains("C:", balloon, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TurnedOffAlertType_ShowsNothing()
    {
        _settings.Current.Notifications.AlertLowDisk = false;
        _inputs.Drives = [LowDrive("C:\\")];
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, _inputs.Calls);
        Assert.Empty(_tray.Balloons);
    }

    [Fact]
    public async Task SeveralAlertsInOneRound_AreSpacedEightSecondsApart()
    {
        _inputs.Drives = [LowDrive("C:\\"), LowDrive("D:\\")];
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.Single(_tray.Balloons);

        await AdvanceAsync(TimeSpan.FromSeconds(7));
        Assert.Single(_tray.Balloons);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(2, _tray.Balloons.Count);
    }

    [Fact]
    public async Task FailingRound_DoesNotEndTheLoop()
    {
        _inputs.FailNextCall = true;
        _inputs.Drives = [LowDrive("C:\\")];
        await StartAndSettleAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.Empty(_tray.Balloons);

        await AdvanceAsync(TimeSpan.FromMinutes(1));
        Assert.Single(_tray.Balloons);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoop()
    {
        await StartAndSettleAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        await _service.StopAsync(CancellationToken.None);
        await AdvanceAsync(TimeSpan.FromMinutes(5));

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
        public int Calls { get; private set; }

        public IReadOnlyList<DriveSnapshot> Drives { get; set; } = [];

        public bool FailNextCall { get; set; }

        public AlertInputs GetInputs()
        {
            Calls++;
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new InvalidOperationException("boom");
            }

            return new AlertInputs(Drives, null, null, false);
        }
    }
}
