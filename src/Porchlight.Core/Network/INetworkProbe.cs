namespace Porchlight.Core.Network;

/// <summary>The four raw checks behind the troubleshooter, behind an interface so the step
/// sequencing and diagnosis are testable without a network.</summary>
public interface INetworkProbe
{
    /// <summary>True when a usable network adapter is up.</summary>
    bool IsAdapterUp();

    /// <summary>True when the router answers a ping (false if there is no router address).</summary>
    Task<bool> PingGatewayAsync(CancellationToken cancellationToken);

    /// <summary>True when a well-known website name resolves to an address.</summary>
    Task<bool> ResolveDnsAsync(CancellationToken cancellationToken);

    /// <summary>Asks Windows' connectivity test address (the one Windows itself uses) for its
    /// expected answer.</summary>
    Task<InternetCheckResult> CheckInternetAsync(CancellationToken cancellationToken);
}
