namespace Porchlight.Core.Safety;

/// <summary>Which remote-control tools are installed or running. Read-only: it never removes anything.</summary>
public interface IRemoteAccessService
{
    Task<RemoteAccessStatus> GetAsync(CancellationToken cancellationToken);
}
