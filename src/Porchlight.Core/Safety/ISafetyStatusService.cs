namespace Porchlight.Core.Safety;

/// <summary>The whole safety picture in one call (security, Windows Update, remote access).</summary>
public interface ISafetyStatusService
{
    /// <summary>Runs the three checks in parallel. Never searches for waiting updates.</summary>
    Task<SafetyStatus> GetAsync(CancellationToken cancellationToken);
}
