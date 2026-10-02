namespace Porchlight.Core.WindowsServices;

/// <summary>Lists every service on the PC (read-only).</summary>
public interface IServiceInfoSource
{
    /// <summary>Returns all services, drivers included; the caller filters. Empty if WMI is unavailable.</summary>
    IReadOnlyList<ServiceRawInfo> ReadAll();
}
