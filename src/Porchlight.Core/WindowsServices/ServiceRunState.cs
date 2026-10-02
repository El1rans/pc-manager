namespace Porchlight.Core.WindowsServices;

/// <summary>Current run state of a service.</summary>
public enum ServiceRunState
{
    Stopped,
    Starting,
    Running,
    Stopping,

    /// <summary>Paused, or any state Porchlight does not show specially.</summary>
    Other,
}
