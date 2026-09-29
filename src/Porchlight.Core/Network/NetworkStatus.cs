namespace Porchlight.Core.Network;

/// <summary>A snapshot of the active network connection shown on the Internet page.</summary>
/// <param name="IsConnected">True when an adapter is up and has a router (gateway) address.</param>
/// <param name="ConnectionType">Wi-Fi, cable or other.</param>
/// <param name="AdapterName">Windows' name for the adapter (the name <c>netsh</c> expects), or null
/// when there is no usable adapter.</param>
/// <param name="Wifi">Wi-Fi details, or null when not on Wi-Fi or Windows would not say.</param>
/// <param name="LocalIp">This PC's IPv4 address on the network, or null.</param>
/// <param name="Gateway">The router's address, or null.</param>
/// <param name="DnsServers">The name-lookup servers in use.</param>
public sealed record NetworkStatus(
    bool IsConnected,
    ConnectionType ConnectionType,
    string? AdapterName,
    WifiInfo? Wifi,
    string? LocalIp,
    string? Gateway,
    IReadOnlyList<string> DnsServers)
{
    /// <summary>Status for a PC with no usable network adapter.</summary>
    public static NetworkStatus Disconnected { get; } =
        new(false, ConnectionType.None, null, null, null, null, []);
}
