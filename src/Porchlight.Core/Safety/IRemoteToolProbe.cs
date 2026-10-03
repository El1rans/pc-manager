namespace Porchlight.Core.Safety;

/// <summary>Collects the raw traces (processes, installed apps, services) the matcher looks at.</summary>
public interface IRemoteToolProbe
{
    /// <summary>Runs off the calling thread. A source that cannot be read is left out, not an error.</summary>
    Task<RemoteToolEvidence> CollectAsync(CancellationToken cancellationToken);
}
