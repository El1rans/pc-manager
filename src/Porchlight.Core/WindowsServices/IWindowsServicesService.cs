namespace Porchlight.Core.WindowsServices;

/// <summary>Lists Windows services and changes the ones that are safe to change. See
/// docs/specs/29-windows-services.md. Every change is checked here, independent of the UI.</summary>
public interface IWindowsServicesService
{
    /// <summary>Lists services (drivers excluded), sorted by display name. Runs off the calling thread.</summary>
    Task<IReadOnlyList<WindowsServiceEntry>> ListAsync(CancellationToken cancellationToken);

    Task<ServiceChangeOutcome> StartAsync(string name, CancellationToken cancellationToken);

    Task<ServiceChangeOutcome> StopAsync(string name, CancellationToken cancellationToken);

    Task<ServiceChangeOutcome> RestartAsync(string name, CancellationToken cancellationToken);

    Task<ServiceChangeOutcome> SetStartTypeAsync(string name, ServiceStartType startType, CancellationToken cancellationToken);
}
