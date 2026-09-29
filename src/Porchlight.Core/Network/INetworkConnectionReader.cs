namespace Porchlight.Core.Network;

/// <summary>Lists the PC's established TCP connections with their owning process.</summary>
public interface INetworkConnectionReader
{
    /// <summary>Returns the connections (IPv4 and IPv6); an empty list if Windows would not say. Never throws.</summary>
    IReadOnlyList<NetworkConnection> ReadConnections();
}
