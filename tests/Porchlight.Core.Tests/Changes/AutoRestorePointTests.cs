using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Changes;
using Porchlight.Core.Health;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Changes;

public sealed class AutoRestorePointTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeSettings _settings = new();
    private readonly FakeRestorePoints _restorePoints = new();

    private AutoRestorePoint Create() => new(_settings, _restorePoints, NullLogger<AutoRestorePoint>.Instance, _time);

    [Fact]
    public void SettingDefaultsToOn() => Assert.True(new AppSettings().Changes.CreateRestorePointBeforeBigChanges);

    [Fact]
    public async Task SettingOff_DoesNothing()
    {
        _settings.Current.Changes.CreateRestorePointBeforeBigChanges = false;

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.SkippedSettingOff, result.Outcome);
        Assert.Equal(0, _restorePoints.StatusReads);
        Assert.Equal(0, _restorePoints.Creates);
    }

    [Fact]
    public async Task Available_CreatesAndSaysSo()
    {
        var result = await Create().EnsureAsync("Porchlight: update 3 apps", TestContext.Current.CancellationToken);

        Assert.True(result.Created);
        Assert.Equal(AutoRestorePoint.CreatedNote, result.Note);
        Assert.Equal(["Porchlight: update 3 apps"], _restorePoints.Descriptions);
    }

    [Fact]
    public async Task ProtectionOff_SkipsWithAQuietNote()
    {
        _restorePoints.Status = new RestorePointStatus(false, RestorePointRules.DefaultFrequencyMinutes, []);

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.SkippedUnavailable, result.Outcome);
        Assert.Equal(AutoRestorePoint.ProtectionOffNote, result.Note);
        Assert.Equal(0, _restorePoints.Creates);
    }

    [Fact]
    public async Task WithinTheFrequencyLimit_SkipsSilently()
    {
        _restorePoints.Status = new RestorePointStatus(
            true,
            RestorePointRules.DefaultFrequencyMinutes,
            [new RestorePointInfo(1, "Recent", _time.GetUtcNow().AddHours(-2))]);

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.SkippedUnavailable, result.Outcome);
        Assert.Null(result.Note);
        Assert.Equal(0, _restorePoints.Creates);
    }

    [Fact]
    public async Task UnreadableStatus_SkipsWithoutAskingWindows()
    {
        _restorePoints.Status = null;

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.SkippedUnavailable, result.Outcome);
        Assert.Equal(0, _restorePoints.Creates);
    }

    [Fact]
    public async Task WindowsRefuses_DoesNotThrowAndReportsFailure()
    {
        _restorePoints.CreateOutcome = RestorePointCreateOutcome.Failed;

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.Failed, result.Outcome);
        Assert.False(result.Created);
    }

    [Fact]
    public async Task ServiceThrowing_NeverBlocksTheChange()
    {
        _restorePoints.Throw = true;

        var result = await Create().EnsureAsync("x", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task SecondRequestSoonAfterIsSkippedUntilTheCooldownPasses()
    {
        var gate = Create();
        await gate.EnsureAsync("one", TestContext.Current.CancellationToken);

        var soon = await gate.EnsureAsync("two", TestContext.Current.CancellationToken);
        _time.Advance(AutoRestorePoint.AttemptCooldown + TimeSpan.FromSeconds(1));
        var later = await gate.EnsureAsync("three", TestContext.Current.CancellationToken);

        Assert.Equal(AutoRestorePointOutcome.SkippedRecentAttempt, soon.Outcome);
        Assert.Equal(AutoRestorePointOutcome.Created, later.Outcome);
        Assert.Equal(["one", "three"], _restorePoints.Descriptions);
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; } = new();

        public void Save()
        {
        }

        public void Update(Action<AppSettings> mutate) => mutate(Current);
    }

    private sealed class FakeRestorePoints : IRestorePointService
    {
        public RestorePointStatus? Status { get; set; } = new(true, RestorePointRules.DefaultFrequencyMinutes, []);

        public RestorePointCreateOutcome CreateOutcome { get; set; } = RestorePointCreateOutcome.Created;

        public bool Throw { get; set; }

        public int StatusReads { get; private set; }

        public int Creates { get; private set; }

        public List<string> Descriptions { get; } = [];

        public Task<HealthReadResult<RestorePointStatus>> GetStatusAsync(CancellationToken cancellationToken)
        {
            StatusReads++;
            if (Throw)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(Status is null
                ? HealthReadResult<RestorePointStatus>.Fail("Couldn't read.")
                : HealthReadResult<RestorePointStatus>.Ok(Status));
        }

        public Task<RestorePointCreateResult> CreateAsync(string description, RestorePointKind kind, CancellationToken cancellationToken)
        {
            Creates++;
            Descriptions.Add(description);
            return Task.FromResult(new RestorePointCreateResult(CreateOutcome, "message"));
        }
    }
}
