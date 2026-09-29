namespace Porchlight.Core.Network;

/// <summary>How the PC is connected to the network, in the terms a user recognises.</summary>
public enum ConnectionType
{
    /// <summary>No active connection.</summary>
    None,

    /// <summary>Wi-Fi (wireless).</summary>
    WiFi,

    /// <summary>Network cable.</summary>
    Ethernet,

    /// <summary>Something else (mobile broadband, a virtual adapter, ...).</summary>
    Other,
}
