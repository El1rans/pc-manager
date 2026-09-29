using Porchlight.Core.Network;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the Internet page.</summary>
public sealed class NetworkSettings
{
    /// <summary>The most recent speed test result, or null if none has been run.</summary>
    public SpeedTestResult? LastSpeedTest { get; set; }
}
