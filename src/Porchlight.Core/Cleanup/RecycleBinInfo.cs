namespace Porchlight.Core.Cleanup;

/// <summary>Size and item count of the Recycle Bin.</summary>
public sealed record RecycleBinInfo(long Bytes, long ItemCount);
