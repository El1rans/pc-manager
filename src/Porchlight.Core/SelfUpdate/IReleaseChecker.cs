namespace Porchlight.Core.SelfUpdate;

/// <summary>Asks GitHub whether a newer Porchlight release than the running one exists.</summary>
public interface IReleaseChecker
{
    /// <summary>Never throws for network or API problems (returns
    /// <see cref="SelfUpdateCheckStatus.CouldNotCheck"/>); only honours <paramref name="cancellationToken"/>.</summary>
    Task<SelfUpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}
