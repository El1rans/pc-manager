namespace Porchlight.Core.WebConsole;

/// <summary>Lists this PC's addresses that another device on the same network could use to reach
/// the web console.</summary>
public interface ILocalAddressProvider
{
    /// <summary>IPv4 addresses of every connected network adapter, excluding loopback and
    /// self-assigned (169.254.x.x) ones. Empty when the PC is not on any network.</summary>
    IReadOnlyList<string> GetAddresses();
}
