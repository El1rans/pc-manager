namespace Porchlight.Core.Network;

/// <summary>Which part of the speed test is running.</summary>
public enum SpeedTestPhase
{
    /// <summary>Measuring delay.</summary>
    Latency,

    /// <summary>Measuring download speed.</summary>
    Download,

    /// <summary>Measuring upload speed.</summary>
    Upload,
}
