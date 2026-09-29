namespace Porchlight.Core.Health;

/// <summary>Reads System Restore state and creates restore points (creating needs administrator rights).</summary>
public interface IRestorePointService
{
    Task<HealthReadResult<RestorePointStatus>> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Asks Windows for a restore point. Never reports success unless a new restore point
    /// actually appeared (Windows silently skips one within its frequency limit).</summary>
    Task<RestorePointCreateResult> CreateAsync(
        string description, RestorePointKind kind, CancellationToken cancellationToken);
}
