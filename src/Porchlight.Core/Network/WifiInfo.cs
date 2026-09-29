namespace Porchlight.Core.Network;

/// <summary>What Windows reports about the current Wi-Fi connection.</summary>
/// <param name="Ssid">Network name, or null when Windows withheld it (on Windows 11 24H2 and later
/// the name needs location permission).</param>
/// <param name="SignalPercent">Signal quality, 0-100.</param>
public sealed record WifiInfo(string? Ssid, int SignalPercent);
