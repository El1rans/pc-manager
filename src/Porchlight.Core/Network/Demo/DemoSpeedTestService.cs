#if DEBUG
namespace Porchlight.Core.Network.Demo;

/// <summary>DEBUG-only <see cref="ISpeedTestService"/> returning a made-up result without any traffic.</summary>
internal sealed class DemoSpeedTestService : ISpeedTestService
{
    private static readonly TimeSpan PhaseDelay = TimeSpan.FromSeconds(1);

    public async Task<SpeedTestResult?> RunAsync(IProgress<SpeedTestPhase>? progress, CancellationToken cancellationToken)
    {
        foreach (var phase in new[] { SpeedTestPhase.Latency, SpeedTestPhase.Download, SpeedTestPhase.Upload })
        {
            progress?.Report(phase);
            await Task.Delay(PhaseDelay, cancellationToken).ConfigureAwait(false);
        }

        return new SpeedTestResult(18, 94.3, 21.7, DateTimeOffset.UtcNow);
    }
}
#endif
