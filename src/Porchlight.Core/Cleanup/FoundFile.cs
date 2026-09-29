namespace Porchlight.Core.Cleanup;

/// <summary>A personal file suggested for removal (never removed automatically).</summary>
/// <param name="FullPath">Full path.</param>
/// <param name="Name">File name.</param>
/// <param name="Folder">Containing folder.</param>
/// <param name="Bytes">Size in bytes.</param>
/// <param name="LastWriteUtc">Last modified time.</param>
public sealed record FoundFile(string FullPath, string Name, string Folder, long Bytes, DateTime LastWriteUtc);
