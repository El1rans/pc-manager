namespace Porchlight.Core.Cleanup;

/// <summary>What <see cref="IDuplicateFinder"/> should look for.</summary>
/// <param name="Roots">Folders to search (de-duplicated by the finder).</param>
/// <param name="MinimumBytes">Smallest file worth comparing.</param>
/// <param name="MaxGroups">How many groups to return, most wasted space first.</param>
public sealed record DuplicateSearchOptions(
    IReadOnlyList<string> Roots,
    long MinimumBytes = DuplicateSearchOptions.DefaultMinimumBytes,
    int MaxGroups = DuplicateSearchOptions.DefaultMaxGroups)
{
    public const long DefaultMinimumBytes = 1024 * 1024;

    public const int DefaultMaxGroups = 200;
}
