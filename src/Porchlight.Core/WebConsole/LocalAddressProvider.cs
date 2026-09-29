using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.WebConsole;

/// <inheritdoc cref="ILocalAddressProvider"/>
public sealed class LocalAddressProvider(ILogger<LocalAddressProvider> logger) : ILocalAddressProvider
{
    public IReadOnlyList<string> GetAddresses()
    {
        var addresses = new List<string>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (IsReachableFromNetwork(unicast.Address))
                    {
                        var text = unicast.Address.ToString();
                        if (!addresses.Contains(text))
                        {
                            addresses.Add(text);
                        }
                    }
                }
            }
        }
        catch (NetworkInformationException ex)
        {
            // Only the address hints on the Web console page are lost; the console itself still runs.
            logger.LogWarning(ex, "Could not list network adapters for the web console address.");
        }

        return addresses;
    }

    /// <summary>Whether another device could plausibly reach this PC at <paramref name="address"/>:
    /// IPv4, not loopback, not self-assigned link-local (169.254.x.x, which Windows picks when no
    /// network answered).</summary>
    public static bool IsReachableFromNetwork(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 169 && bytes[1] == 254);
    }
}
