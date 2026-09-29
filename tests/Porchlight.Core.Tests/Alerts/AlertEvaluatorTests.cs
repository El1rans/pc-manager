using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Alerts;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.Core.Tests.Alerts;

public sealed class AlertEvaluatorTests
{
    private const long Gb = 1024L * 1024 * 1024;

    private static readonly AlertPreferences AllOn = new(true, true, true, true);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeStateStore _state = new();
    private readonly AlertEvaluator _evaluator;

    public AlertEvaluatorTests()
    {
        _evaluator = new AlertEvaluator(_time, _state);
    }

    private static DriveSnapshot Drive(string name, long freeGb, long totalGb = 500) =>
        new(name, null, "NTFS", totalGb * Gb, freeGb * Gb, false);

    private static AlertInputs Inputs(
        IReadOnlyList<DriveSnapshot>? drives = null, double? cpu = null, double? gpu = null, bool restart = false) =>
        new(drives ?? [], cpu, gpu, restart);

    [Fact]
    public void LowDrive_Alerts_AndHealthyDrive_DoesNot()
    {
        var alerts = _evaluator.Evaluate(Inputs([Drive("C:\\", 2), Drive("D:\\", 300)]), AllOn);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertKind.LowDisk, alert.Kind);
        Assert.Equal("C:\\", alert.Subject);
        Assert.Equal(AlertTarget.Dashboard, alert.Target);
        Assert.Contains("C:", alert.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void LowDisk_IsThrottledForTwentyFourHours_ThenFiresAgain()
    {
        var inputs = Inputs([Drive("C:\\", 2)]);
        Assert.Single(_evaluator.Evaluate(inputs, AllOn));

        _time.Advance(TimeSpan.FromHours(23));
        Assert.Empty(_evaluator.Evaluate(inputs, AllOn));

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Single(_evaluator.Evaluate(inputs, AllOn));
    }

    [Fact]
    public void LowDisk_ThrottleIsPerDrive()
    {
        Assert.Single(_evaluator.Evaluate(Inputs([Drive("C:\\", 2)]), AllOn));

        var alerts = _evaluator.Evaluate(Inputs([Drive("C:\\", 2), Drive("D:\\", 1)]), AllOn);

        Assert.Equal("D:\\", Assert.Single(alerts).Subject);
    }

    [Fact]
    public void Throttle_SurvivesANewEvaluatorSharingTheSameStore()
    {
        var inputs = Inputs([Drive("C:\\", 2)]);
        Assert.Single(_evaluator.Evaluate(inputs, AllOn));

        var restarted = new AlertEvaluator(_time, _state);

        Assert.Empty(restarted.Evaluate(inputs, AllOn));
    }

    [Fact]
    public void DisabledAlert_DoesNotFire_AndDoesNotConsumeThrottle()
    {
        var inputs = Inputs([Drive("C:\\", 2)]);
        Assert.Empty(_evaluator.Evaluate(inputs, AllOn with { LowDisk = false }));

        Assert.Single(_evaluator.Evaluate(inputs, AllOn));
    }

    [Fact]
    public void Temperature_RequiresSustainedHeat()
    {
        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));

        _time.Advance(TimeSpan.FromMinutes(1));
        var alert = Assert.Single(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));
        Assert.Equal(AlertKind.HighTemperature, alert.Kind);
        Assert.Equal("cpu", alert.Subject);
        Assert.Equal(AlertTarget.Hardware, alert.Target);
        Assert.Contains("processor", alert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Temperature_DroppingBelowThreshold_ResetsTheTimer()
    {
        _evaluator.Evaluate(Inputs(cpu: 95), AllOn);
        _time.Advance(TimeSpan.FromMinutes(2));
        _evaluator.Evaluate(Inputs(cpu: 60), AllOn);
        _time.Advance(TimeSpan.FromMinutes(2));

        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));
    }

    [Fact]
    public void Temperature_UnavailableReadings_NeverAlert_AndResetTheTimer()
    {
        _evaluator.Evaluate(Inputs(cpu: 95), AllOn);
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: null, gpu: null), AllOn));
        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));
    }

    [Fact]
    public void Temperature_GpuUsesItsOwnLowerThreshold()
    {
        _evaluator.Evaluate(Inputs(cpu: 88, gpu: 88), AllOn);
        _time.Advance(TimeSpan.FromMinutes(3));

        var alert = Assert.Single(_evaluator.Evaluate(Inputs(cpu: 88, gpu: 88), AllOn));

        Assert.Equal("gpu", alert.Subject);
        Assert.Contains("graphics card", alert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Temperature_Disabled_NeverAlerts()
    {
        var off = AllOn with { Temperature = false };
        _evaluator.Evaluate(Inputs(cpu: 99), off);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 99), off));
    }

    [Fact]
    public void Temperature_IsThrottledAfterFiring()
    {
        _evaluator.Evaluate(Inputs(cpu: 95), AllOn);
        _time.Advance(TimeSpan.FromMinutes(3));
        Assert.Single(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));

        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty(_evaluator.Evaluate(Inputs(cpu: 95), AllOn));
    }

    [Fact]
    public void Restart_FirstSeen_IsRecorded_ButNotAlertedUntilOverThreeDays()
    {
        Assert.Empty(_evaluator.Evaluate(Inputs(restart: true), AllOn));
        Assert.Equal(_time.GetUtcNow(), _state.RestartSince);

        _time.Advance(TimeSpan.FromDays(3));
        Assert.Empty(_evaluator.Evaluate(Inputs(restart: true), AllOn));

        _time.Advance(TimeSpan.FromHours(1));
        var alert = Assert.Single(_evaluator.Evaluate(Inputs(restart: true), AllOn));
        Assert.Equal(AlertKind.RestartPending, alert.Kind);
        Assert.Contains("3 days", alert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Restart_ClearedWhenNoLongerPending()
    {
        _evaluator.Evaluate(Inputs(restart: true), AllOn);

        _evaluator.Evaluate(Inputs(restart: false), AllOn);

        Assert.Null(_state.RestartSince);
    }

    [Fact]
    public void Restart_WaitIsTrackedEvenWhileAlertIsOff()
    {
        var off = AllOn with { RestartPending = false };
        _evaluator.Evaluate(Inputs(restart: true), off);
        _time.Advance(TimeSpan.FromDays(4));
        Assert.Empty(_evaluator.Evaluate(Inputs(restart: true), off));

        Assert.Single(_evaluator.Evaluate(Inputs(restart: true), AllOn));
    }

    [Fact]
    public void Updates_AlertsWithCount_AndIsThrottled()
    {
        var alert = _evaluator.EvaluateUpdates(3, AllOn);

        Assert.NotNull(alert);
        Assert.Equal("3 app updates are ready", alert.Title);
        Assert.Equal(AlertTarget.Updates, alert.Target);
        Assert.Null(_evaluator.EvaluateUpdates(3, AllOn));

        _time.Advance(TimeSpan.FromHours(24));
        Assert.NotNull(_evaluator.EvaluateUpdates(3, AllOn));
    }

    [Fact]
    public void Updates_SingularTitle_ZeroOrDisabled_ReturnsNull()
    {
        Assert.Null(_evaluator.EvaluateUpdates(0, AllOn));
        Assert.Null(_evaluator.EvaluateUpdates(2, AllOn with { Updates = false }));
        Assert.Equal("1 app update is ready", _evaluator.EvaluateUpdates(1, AllOn)?.Title);
    }

    [Fact]
    public void ClockMovingBackwards_DoesNotMuteAlertsForever()
    {
        var inputs = Inputs([Drive("C:\\", 2)]);

        // A last-fired time "in the future" is what a clock moved backwards looks like.
        _state.SetLastFired(AlertEvaluator.BuildKey(AlertKind.LowDisk, "C:\\"), _time.GetUtcNow().AddDays(2));

        Assert.Single(_evaluator.Evaluate(inputs, AllOn));
    }

    private sealed class FakeStateStore : IAlertStateStore
    {
        private readonly Dictionary<string, DateTimeOffset> _fired = [];

        public DateTimeOffset? RestartSince { get; private set; }

        public DateTimeOffset? GetLastFired(string key) => _fired.TryGetValue(key, out var w) ? w : null;

        public void SetLastFired(string key, DateTimeOffset when) => _fired[key] = when;

        public DateTimeOffset? GetRestartPendingSince() => RestartSince;

        public void SetRestartPendingSince(DateTimeOffset? when) => RestartSince = when;
    }
}
