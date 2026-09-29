namespace Porchlight.Core.Network;

/// <summary>A speed test result in plain words.</summary>
/// <param name="Level">Overall level.</param>
/// <param name="Headline">Short label, e.g. "Good".</param>
/// <param name="Detail">One or two plain sentences about what the speed is good for.</param>
public sealed record SpeedVerdict(SpeedVerdictLevel Level, string Headline, string Detail);
