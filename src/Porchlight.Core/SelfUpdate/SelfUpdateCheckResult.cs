namespace Porchlight.Core.SelfUpdate;

/// <summary>Outcome of a check for a newer Porchlight release.</summary>
public enum SelfUpdateCheckStatus
{
    /// <summary>The newest published release is not newer than the running version.</summary>
    UpToDate,

    /// <summary>A newer release exists - see <see cref="SelfUpdateCheckResult.Release"/>.</summary>
    UpdateAvailable,

    /// <summary>The check couldn't complete (offline, rate limited, unexpected reply). Not an error to show.</summary>
    CouldNotCheck,
}

/// <param name="Status">What the check found.</param>
/// <param name="Release">The newer release when <paramref name="Status"/> is <see cref="SelfUpdateCheckStatus.UpdateAvailable"/>.</param>
public sealed record SelfUpdateCheckResult(SelfUpdateCheckStatus Status, ReleaseInfo? Release = null)
{
    public static SelfUpdateCheckResult UpToDate { get; } = new(SelfUpdateCheckStatus.UpToDate);

    public static SelfUpdateCheckResult CouldNotCheck { get; } = new(SelfUpdateCheckStatus.CouldNotCheck);
}
