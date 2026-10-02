namespace Porchlight.Core.WindowsServices;

/// <summary>Outcome of changing a service.</summary>
public enum ServiceChangeResult
{
    Changed,

    /// <summary>The service no longer exists.</summary>
    NotFound,

    /// <summary>Not allowed: a Windows service, a service Porchlight manages elsewhere, or a name
    /// that was not in the last listing. Nothing was touched.</summary>
    Refused,

    /// <summary>Porchlight is not running as administrator; nothing was attempted.</summary>
    NeedsAdmin,

    /// <summary>Stop was refused because other running services depend on this one.</summary>
    HasDependents,

    /// <summary>The service did not reach the wanted state in time.</summary>
    TimedOut,

    /// <summary>Any other failure (logged).</summary>
    Failed,
}
