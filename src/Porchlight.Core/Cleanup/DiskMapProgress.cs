namespace Porchlight.Core.Cleanup;

/// <summary>A progress update from <see cref="IDiskSpaceMapper"/>: the folder being read and how much
/// has been counted so far.</summary>
public sealed record DiskMapProgress(string CurrentFolder, long Bytes, long Files);
