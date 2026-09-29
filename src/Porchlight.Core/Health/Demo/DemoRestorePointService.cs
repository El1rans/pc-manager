namespace Porchlight.Core.Health.Demo;

/// <summary>DEBUG demo data: sample restore points; "creating" one only reports success.</summary>
internal sealed class DemoRestorePointService : IRestorePointService
{
    public Task<HealthReadResult<RestorePointStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        IReadOnlyList<RestorePointInfo> points =
        [
            new(3, "Windows Update", now.AddDays(-2)),
            new(2, "Installed Demo App", now.AddDays(-9)),
            new(1, "Automatic Restore Point", now.AddDays(-20)),
        ];
        return Task.FromResult(HealthReadResult<RestorePointStatus>.Ok(
            new RestorePointStatus(true, RestorePointRules.DefaultFrequencyMinutes, points)));
    }

    public Task<RestorePointCreateResult> CreateAsync(
        string description, RestorePointKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(new RestorePointCreateResult(RestorePointCreateOutcome.Created, "Restore point created."));
}
