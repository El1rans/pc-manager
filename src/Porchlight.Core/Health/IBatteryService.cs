namespace Porchlight.Core.Health;

/// <summary>Reads the battery, if the device has one.</summary>
public interface IBatteryService
{
    /// <summary>A successful result with a <see langword="null"/> value means "no battery in this
    /// device" - the Battery card stays hidden.</summary>
    Task<HealthReadResult<BatteryReading?>> GetAsync(CancellationToken cancellationToken);
}
