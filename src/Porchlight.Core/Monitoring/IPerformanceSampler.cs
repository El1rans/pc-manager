namespace Porchlight.Core.Monitoring;

/// <summary>Samples CPU, memory, GPU, disk and network activity. Owns its performance counters
/// (created once, primed, disposed with the sampler); call <see cref="Sample"/> roughly once a
/// second from a background thread.</summary>
public interface IPerformanceSampler : IDisposable
{
    /// <summary>Takes a full sample, first running the one-time counter setup (about a second) if it
    /// has not run yet.</summary>
    PerformanceSnapshot Sample();

    /// <summary>True once the one-time counter setup has finished, so GPU and disk values in
    /// <see cref="SampleWithoutWaiting"/> are real rather than still warming up.</summary>
    bool IsWarmedUp => true;

    /// <summary>Runs the one-time counter setup now (blocking), so a later sample does not pay for
    /// it. Safe to call from a background thread while another thread samples.</summary>
    void WarmUp()
    {
    }

    /// <summary>Like <see cref="Sample"/>, but never waits for the counter setup: until
    /// <see cref="IsWarmedUp"/>, returns CPU (from a quicker source), memory and network, with the
    /// counter-only values (GPU, disk) null.</summary>
    PerformanceSnapshot SampleWithoutWaiting() => Sample();
}
