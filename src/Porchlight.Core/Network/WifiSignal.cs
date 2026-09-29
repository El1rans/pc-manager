namespace Porchlight.Core.Network;

/// <summary>A Wi-Fi signal strength in the two forms the UI shows: bars and words.</summary>
/// <param name="Bars">0 to 4.</param>
/// <param name="Label">Plain-language word, e.g. "Good".</param>
public sealed record WifiSignal(int Bars, string Label);
