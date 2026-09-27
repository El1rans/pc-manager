namespace PCManager.Core.Hardware;

/// <summary>Where hardware access currently stands, per spec 04 ("Amended after 01b"): access
/// depends on running elevated AND the <c>pawnio</c> component being installed, not on LHM alone.</summary>
public enum HardwareStatus
{
    /// <summary>Not running as administrator; only vendor-API/WMI-readable sensors are available.</summary>
    NotElevated,

    /// <summary>Elevated, but the PawnIO driver component is not installed yet.</summary>
    DriverMissing,

    /// <summary>Elevated and the driver is installed; full sensor and fan-control access.</summary>
    Ready,

    /// <summary>Something went wrong reading hardware; see <see cref="HardwareSnapshot.Message"/>.</summary>
    Error,
}
