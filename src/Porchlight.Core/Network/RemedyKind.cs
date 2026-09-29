namespace Porchlight.Core.Network;

/// <summary>A fix the troubleshooter can suggest.</summary>
public enum RemedyKind
{
    /// <summary>Clear the saved website addresses (<c>ipconfig /flushdns</c>).</summary>
    FlushDns,

    /// <summary>Ask the router for a fresh address (<c>ipconfig /release</c> and <c>/renew</c>).</summary>
    RenewIp,

    /// <summary>Switch the network adapter off and on again. Needs administrator rights.</summary>
    ResetAdapter,

    /// <summary>Only offered, never run: opens Windows' Network settings page, where the user can
    /// choose "Network reset" themselves as a last resort.</summary>
    OpenNetworkSettings,
}
