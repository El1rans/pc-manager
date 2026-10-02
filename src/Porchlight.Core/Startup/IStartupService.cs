namespace Porchlight.Core.Startup;

/// <summary>Lists what starts at sign-in and turns items on or off (reversibly, like Task Manager).</summary>
public interface IStartupService
{
    /// <summary>Enumerates every startup item, off the calling thread. Also defines which ids
    /// <see cref="SetEnabledAsync"/> will accept.</summary>
    Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken);

    /// <summary>True when the last listing couldn't read Windows' startup trace without administrator
    /// rights, so every entry's impact is <see cref="StartupImpact.NotMeasured"/>.</summary>
    bool ImpactNeedsAdmin { get; }

    /// <summary>Turns the entry with <paramref name="entryId"/> on or off by writing its
    /// <c>StartupApproved</c> value. Never deletes or edits the entry itself.</summary>
    Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken);
}
