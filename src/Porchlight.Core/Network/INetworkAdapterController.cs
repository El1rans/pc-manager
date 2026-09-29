namespace Porchlight.Core.Network;

/// <summary>Switches a network adapter off and on, identified by its interface GUID rather than
/// its display name (names with spaces are unreliable in <c>netsh</c>). Behind an interface so the
/// "enable always follows disable" rule can be tested with a fake.</summary>
public interface INetworkAdapterController
{
    /// <summary>Disables the adapter. Returns true on success; throws or times out on failure.</summary>
    Task<bool> DisableAsync(string adapterId, CancellationToken cancellationToken);

    /// <summary>Enables the adapter. Returns true on success; throws or times out on failure.</summary>
    Task<bool> EnableAsync(string adapterId, CancellationToken cancellationToken);
}
