using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkStatusProvider"/>
public sealed class NetworkStatusProvider : INetworkStatusProvider
{
    private const int MaxDnsServers = 4;

    private static readonly string[] VirtualAdapterMarkers =
        ["virtual", "vmware", "hyper-v", "vpn", "loopback", "bluetooth", "tap-"];

    private readonly IWifiInfoReader _wifiInfoReader;
    private readonly ILogger<NetworkStatusProvider> _logger;

    public NetworkStatusProvider(IWifiInfoReader wifiInfoReader, ILogger<NetworkStatusProvider> logger)
    {
        _wifiInfoReader = wifiInfoReader;
        _logger = logger;
    }

    public NetworkStatus GetStatus()
    {
        try
        {
            var best = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsCandidate)
                .Select(nic => (Nic: nic, Properties: nic.GetIPProperties()))
                .OrderByDescending(x => FindGateway(x.Properties) is not null)
                .ThenBy(x => LooksVirtual(x.Nic))
                .FirstOrDefault();

            if (best.Nic is null)
            {
                return NetworkStatus.Disconnected;
            }

            var gateway = FindGateway(best.Properties);
            var localIp = best.Properties.UnicastAddresses
                .Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            var dns = best.Properties.DnsAddresses
                .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                .Select(a => a.ToString())
                .Take(MaxDnsServers)
                .ToList();

            var type = MapType(best.Nic.NetworkInterfaceType);
            var wifi = type == ConnectionType.WiFi ? _wifiInfoReader.Read() : null;

            return new NetworkStatus(gateway is not null, type, best.Nic.Name, wifi, localIp, gateway, dns);
        }
        catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException
                                       or PlatformNotSupportedException)
        {
            // Nothing sensible to show; the page reads this as "not connected". Safe to ignore
            // because the user can hit Refresh, and the troubleshooter re-checks independently.
            _logger.LogDebug(ex, "Could not read the network interfaces.");
            return NetworkStatus.Disconnected;
        }
    }

    private static bool IsCandidate(NetworkInterface nic) =>
        nic.OperationalStatus == OperationalStatus.Up
        && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel);

    private static bool LooksVirtual(NetworkInterface nic) =>
        VirtualAdapterMarkers.Any(m => nic.Description.Contains(m, StringComparison.OrdinalIgnoreCase)
                                       || nic.Name.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static string? FindGateway(IPInterfaceProperties properties) =>
        properties.GatewayAddresses
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))
            ?.ToString();

    private static ConnectionType MapType(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => ConnectionType.WiFi,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => ConnectionType.Ethernet,
        _ => ConnectionType.Other,
    };
}
