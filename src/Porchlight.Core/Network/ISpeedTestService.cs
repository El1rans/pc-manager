namespace Porchlight.Core.Network;

/// <summary>Measures latency, download and upload speed against Cloudflare's speed test service.
/// Only ever run because the user pressed the button.</summary>
public interface ISpeedTestService
{
    /// <summary>Runs the test (about 10 seconds), reporting each phase as it starts. Returns null
    /// if nothing could be measured. Throws <see cref="OperationCanceledException"/> if cancelled.</summary>
    Task<SpeedTestResult?> RunAsync(IProgress<SpeedTestPhase>? progress, CancellationToken cancellationToken);
}
