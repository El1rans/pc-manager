namespace Porchlight.Core.Monitoring;

/// <summary>
/// Turns two per-adapter network samples into total download/upload byte deltas, matching adapters
/// by <see cref="NetworkAdapterSample.AdapterId"/> rather than assuming a stable enumeration order.
/// A pure function over sample lists so it is unit-testable without touching real network adapters.
/// </summary>
public static class NetworkThroughputCalculator
{
    /// <summary>
    /// Sums per-adapter deltas for every adapter present in <paramref name="current"/>.
    /// - An adapter new in <paramref name="current"/> (not in <paramref name="previous"/>) contributes
    ///   nothing this tick (no baseline yet) rather than counting its whole lifetime total.
    /// - An adapter that disappeared (present in <paramref name="previous"/> but not
    ///   <paramref name="current"/>) is simply absent from the loop and contributes nothing.
    /// - A negative per-adapter delta (the adapter's counters reset, e.g. driver restart) clamps to
    ///   zero for that adapter only, rather than going negative or discarding the whole sample.
    /// </summary>
    public static (long DownloadBytes, long UploadBytes) CalculateDelta(
        IReadOnlyDictionary<string, NetworkAdapterSample> previous,
        IReadOnlyList<NetworkAdapterSample> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        long downloadBytes = 0;
        long uploadBytes = 0;

        foreach (var sample in current)
        {
            if (!previous.TryGetValue(sample.AdapterId, out var previousSample))
            {
                continue;
            }

            downloadBytes += Math.Max(0, sample.BytesReceived - previousSample.BytesReceived);
            uploadBytes += Math.Max(0, sample.BytesSent - previousSample.BytesSent);
        }

        return (downloadBytes, uploadBytes);
    }
}
