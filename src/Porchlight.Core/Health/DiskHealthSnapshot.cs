namespace Porchlight.Core.Health;

/// <summary>Everything the Disk health card shows.</summary>
/// <param name="Disks">One entry per physical disk.</param>
/// <param name="PredictFailureDetected">Windows' failure prediction fired but could not be pinned
/// on one specific disk (several disks present) - shown as a card-level warning.</param>
public sealed record DiskHealthSnapshot(IReadOnlyList<DiskHealthReport> Disks, bool PredictFailureDetected);
