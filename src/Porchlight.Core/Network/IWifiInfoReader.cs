namespace Porchlight.Core.Network;

/// <summary>Reads the current Wi-Fi connection's name and signal from Windows.</summary>
public interface IWifiInfoReader
{
    /// <summary>Returns the Wi-Fi details, or null when there is no Wi-Fi connection or Windows
    /// would not provide it; never throws.</summary>
    WifiInfo? Read();
}
