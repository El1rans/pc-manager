namespace Porchlight.Core.Network;

/// <summary>Reads the current network status. Cheap and synchronous, but call it off the UI
/// thread (it makes a few Windows API calls).</summary>
public interface INetworkStatusProvider
{
    /// <summary>Returns the status of the best active connection; never throws.</summary>
    NetworkStatus GetStatus();
}
