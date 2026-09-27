namespace PCManager.Core.Monitoring;

/// <summary>Samples CPU, memory, GPU, disk and network activity. Owns its performance counters
/// (created once, primed, disposed with the sampler); call <see cref="Sample"/> roughly once a
/// second from a background thread.</summary>
public interface IPerformanceSampler : IDisposable
{
    PerformanceSnapshot Sample();
}
