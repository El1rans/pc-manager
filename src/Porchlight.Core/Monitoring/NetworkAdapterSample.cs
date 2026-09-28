namespace Porchlight.Core.Monitoring;

/// <summary>One network adapter's cumulative byte counters at a point in time.</summary>
/// <param name="AdapterId">The adapter's stable id (<c>NetworkInterface.Id</c>), used to match
/// samples of the same adapter across ticks even if adapters are enumerated in a different order.</param>
/// <param name="BytesReceived">Cumulative bytes received since the adapter came up.</param>
/// <param name="BytesSent">Cumulative bytes sent since the adapter came up.</param>
public readonly record struct NetworkAdapterSample(string AdapterId, long BytesReceived, long BytesSent);
