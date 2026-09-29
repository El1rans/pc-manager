namespace Porchlight.Core.Health.Demo;

/// <summary>DEBUG demo data: a somewhat worn laptop battery.</summary>
internal sealed class DemoBatteryService : IBatteryService
{
    public Task<HealthReadResult<BatteryReading?>> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(HealthReadResult<BatteryReading?>.Ok(
            new BatteryReading(50_000, 30_000, 412, 76, BatteryChargeState.Discharging)));
}
