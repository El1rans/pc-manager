namespace PCManager.Core.Monitoring;

/// <summary>Reads one-off machine identity/hardware facts via WMI. Runs off the UI thread; any
/// single query failing leaves that field "Unknown" rather than failing the whole call.</summary>
public interface ISystemInfoProvider
{
    Task<SystemInfo> GetAsync(CancellationToken cancellationToken);
}
