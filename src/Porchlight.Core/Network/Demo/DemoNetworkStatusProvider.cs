#if DEBUG
namespace Porchlight.Core.Network.Demo;

/// <summary>DEBUG-only fake <see cref="INetworkStatusProvider"/> - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoNetworkStatusProvider : INetworkStatusProvider
{
    public NetworkStatus GetStatus() => new(
        true, ConnectionType.WiFi, "Wi-Fi", new WifiInfo("Home Network", 82),
        "192.168.1.23", "192.168.1.1", ["192.168.1.1", "8.8.8.8"]);
}
#endif
