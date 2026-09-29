namespace Porchlight.Core.Network;

/// <summary>The four checks of "Fix my internet", in the order they run.</summary>
public enum TroubleshootStep
{
    /// <summary>Is the network adapter (Wi-Fi card or cable port) switched on and connected?</summary>
    Adapter,

    /// <summary>Can this PC reach the router?</summary>
    Router,

    /// <summary>Can this PC turn a website name into an address (DNS)?</summary>
    NameLookup,

    /// <summary>Can this PC actually reach the internet?</summary>
    Internet,
}
