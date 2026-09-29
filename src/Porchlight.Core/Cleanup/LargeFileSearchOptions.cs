namespace Porchlight.Core.Cleanup;

/// <summary>What <see cref="ILargeFileFinder"/> should look for.</summary>
/// <param name="Roots">Folders to search (de-duplicated by the finder; nested ones are covered by
/// their parent).</param>
/// <param name="MinimumBytes">Smallest file worth listing.</param>
/// <param name="MaxResults">How many files to return, largest first.</param>
/// <param name="Extensions">If not null, only files with one of these extensions (with the dot,
/// case-insensitive).</param>
/// <param name="MinimumAge">If not null, only files last modified at least this long ago.</param>
public sealed record LargeFileSearchOptions(
    IReadOnlyList<string> Roots,
    long MinimumBytes,
    int MaxResults,
    IReadOnlyList<string>? Extensions = null,
    TimeSpan? MinimumAge = null)
{
    public const int DefaultMaxResults = 50;

    public const long DefaultBigFileMegabytes = 500;

    public const long OldDownloadMinimumBytes = 10L * 1024 * 1024;

    public static readonly TimeSpan OldDownloadAge = TimeSpan.FromDays(30);

    public static readonly IReadOnlyList<string> InstallerAndArchiveExtensions =
        [".exe", ".msi", ".zip", ".7z", ".rar", ".iso"];

    /// <summary>Big personal files: everything in the user's own folders at or above the threshold.</summary>
    public static LargeFileSearchOptions BigFiles(IReadOnlyList<string> personalFolders, long thresholdMegabytes) =>
        new(personalFolders, thresholdMegabytes * 1024 * 1024, DefaultMaxResults);

    /// <summary>Old downloads: installers and archives in Downloads untouched for 30 days, 10 MB or bigger.</summary>
    public static LargeFileSearchOptions OldDownloads(string downloadsFolder) =>
        new([downloadsFolder], OldDownloadMinimumBytes, DefaultMaxResults, InstallerAndArchiveExtensions, OldDownloadAge);
}
