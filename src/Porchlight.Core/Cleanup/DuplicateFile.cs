namespace Porchlight.Core.Cleanup;

/// <summary>One copy in a <see cref="DuplicateGroup"/>.</summary>
/// <param name="FullPath">Full path.</param>
/// <param name="Name">File name.</param>
/// <param name="Folder">Containing folder.</param>
/// <param name="LastWriteUtc">Last modified time, as scanned.</param>
public sealed record DuplicateFile(string FullPath, string Name, string Folder, DateTime LastWriteUtc);
