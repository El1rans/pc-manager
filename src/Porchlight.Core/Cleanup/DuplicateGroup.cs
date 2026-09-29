namespace Porchlight.Core.Cleanup;

/// <summary>Two or more byte-identical files.</summary>
/// <param name="FileBytes">Size of each copy.</param>
/// <param name="Files">The copies, newest first.</param>
public sealed record DuplicateGroup(long FileBytes, IReadOnlyList<DuplicateFile> Files)
{
    /// <summary>Space that would be freed by keeping just one copy.</summary>
    public long WastedBytes => FileBytes * (Files.Count - 1);
}
