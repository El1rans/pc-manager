namespace Porchlight.Core.Network;

/// <summary>Runs the fixes the troubleshooter can suggest. Callers must have shown the user any
/// warning and got confirmation first; none of these throw for an ordinary failure.</summary>
public interface INetworkRemedyService
{
    /// <summary>Clears the saved website addresses.</summary>
    Task<RemedyResult> FlushDnsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a fresh address from the router. The connection drops for a few seconds.</summary>
    Task<RemedyResult> RenewIpAsync(CancellationToken cancellationToken);

    /// <summary>Switches the adapter with the given interface GUID off and on. Needs administrator rights.</summary>
    Task<RemedyResult> ResetAdapterAsync(string adapterId, CancellationToken cancellationToken);
}
