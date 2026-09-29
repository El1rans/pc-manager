namespace Porchlight.Core.Cleanup;

/// <summary>A file listed inside a <see cref="DiskNode"/> on the disk space map.</summary>
/// <param name="FullPath">Full path.</param>
/// <param name="Name">File name.</param>
/// <param name="Bytes">Size in bytes.</param>
/// <param name="LastWriteUtc">Last modified time.</param>
/// <param name="CanRecycle">True only for ordinary files inside the user's personal folders; the
/// map never offers the Recycle Bin for anything else.</param>
public sealed record DiskFile(string FullPath, string Name, long Bytes, DateTime LastWriteUtc, bool CanRecycle);
