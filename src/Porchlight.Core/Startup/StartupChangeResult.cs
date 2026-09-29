namespace Porchlight.Core.Startup;

/// <summary>Outcome of <see cref="IStartupService.SetEnabledAsync"/>.</summary>
public enum StartupChangeResult
{
    /// <summary>The <c>StartupApproved</c> value was written.</summary>
    Changed,

    /// <summary>The id was not one of the entries returned by the last listing; nothing was touched.</summary>
    NotFound,

    /// <summary>A per-machine entry while not elevated; nothing was written.</summary>
    NeedsAdmin,

    /// <summary>The write failed (logged).</summary>
    Failed,
}
