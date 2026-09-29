namespace Porchlight.Core.Health;

/// <summary>Reads the health of every physical disk (never throws for an ordinary WMI failure).</summary>
public interface IDiskHealthService
{
    Task<HealthReadResult<DiskHealthSnapshot>> GetAsync(CancellationToken cancellationToken);
}
