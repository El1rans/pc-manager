using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <summary>Builds the v1 list of cleanup categories from an <see cref="ICleanupPathProvider"/>. Any
/// root that resolves to something dangerous (empty, relative, a drive root, the Windows folder, the
/// user profile, Program Files, ...) is dropped, so a broken environment variable can never turn
/// into "delete everything under C:\" - see docs/specs/12-disk-cleanup.md, safety rule 3.</summary>
public sealed partial class CleanupCatalog : ICleanupCatalog
{
    private static readonly TimeSpan TempMinimumAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan WindowsUpdateMinimumAge = TimeSpan.FromDays(7);

    private static readonly string[] ChromiumCacheFolders = ["Cache", "Code Cache", "GPUCache"];

    private static readonly (string DisplayName, string ProcessName, string[] UserDataRelativePath)[] ChromiumBrowsers =
    [
        ("Microsoft Edge", "msedge", ["Microsoft", "Edge", "User Data"]),
        ("Google Chrome", "chrome", ["Google", "Chrome", "User Data"]),
        ("Brave", "brave", ["BraveSoftware", "Brave-Browser", "User Data"]),
    ];

    private readonly ICleanupPathProvider _paths;
    private readonly ICleanupFileSystem _fileSystem;
    private readonly ILogger<CleanupCatalog> _logger;

    public CleanupCatalog(ICleanupPathProvider paths, ICleanupFileSystem fileSystem, ILogger<CleanupCatalog> logger)
    {
        _paths = paths;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public IReadOnlyList<CleanupCategory> GetCategories()
    {
        var localAppData = _paths.LocalAppData;
        var windows = _paths.WindowsDirectory;
        var programData = _paths.ProgramData;

        var candidates = new List<CleanupCategory>
        {
            new(
                CleanupCategoryId.TemporaryFiles,
                "Temporary files",
                "Scraps that programs leave behind while they work. Safe to remove.",
                RequiresAdmin: false, IsSelectedByDefault: true,
                Roots: [new CleanupRoot(_paths.TempPath)],
                FileNamePattern: null, MinimumAge: TempMinimumAge),

            new(
                CleanupCategoryId.WindowsTemporaryFiles,
                "Windows temporary files",
                "Scraps that Windows itself leaves behind. Safe to remove.",
                RequiresAdmin: true, IsSelectedByDefault: true,
                Roots: [new CleanupRoot(CombineOrEmpty(windows, "Temp"), RequiresAdmin: true)],
                FileNamePattern: null, MinimumAge: TempMinimumAge),

            new(
                CleanupCategoryId.BrowserCaches,
                "Browser caches",
                "Saved copies of web pages that speed up browsing. Your bookmarks, history and passwords are not touched. Close the browser first.",
                RequiresAdmin: false, IsSelectedByDefault: true,
                Roots: BuildBrowserCacheRoots(localAppData),
                FileNamePattern: null, MinimumAge: TimeSpan.Zero),

            new(
                CleanupCategoryId.ThumbnailCache,
                "Picture previews",
                "Small saved previews of your pictures and videos. Windows rebuilds them when needed, which can make folders load a little slower at first.",
                RequiresAdmin: false, IsSelectedByDefault: false,
                Roots: [new CleanupRoot(CombineOrEmpty(localAppData, "Microsoft", "Windows", "Explorer"))],
                FileNamePattern: "thumbcache_*.db", MinimumAge: TimeSpan.Zero),

            new(
                CleanupCategoryId.CrashReports,
                "Crash reports and dumps",
                "Technical reports that programs and Windows save after a crash. Safe to remove.",
                RequiresAdmin: false, IsSelectedByDefault: true,
                Roots:
                [
                    new CleanupRoot(CombineOrEmpty(localAppData, "CrashDumps")),
                    new CleanupRoot(CombineOrEmpty(localAppData, "Microsoft", "Windows", "WER")),
                    new CleanupRoot(CombineOrEmpty(programData, "Microsoft", "Windows", "WER"), RequiresAdmin: true),
                ],
                FileNamePattern: null, MinimumAge: TimeSpan.Zero),

            new(
                CleanupCategoryId.WindowsUpdateLeftovers,
                "Windows Update leftovers",
                "Downloaded update files that Windows has already installed. Safe to remove.",
                RequiresAdmin: true, IsSelectedByDefault: true,
                Roots: [new CleanupRoot(CombineOrEmpty(windows, "SoftwareDistribution", "Download"), RequiresAdmin: true)],
                FileNamePattern: null, MinimumAge: WindowsUpdateMinimumAge),

            new(
                CleanupCategoryId.DeliveryOptimizationCache,
                "Windows update sharing cache",
                "Copies of updates Windows keeps to share with other PCs. Windows downloads them again if needed.",
                RequiresAdmin: true, IsSelectedByDefault: false,
                Roots:
                [
                    new CleanupRoot(
                        CombineOrEmpty(
                            windows, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows",
                            "DeliveryOptimization", "Cache"),
                        RequiresAdmin: true),
                ],
                FileNamePattern: null, MinimumAge: TimeSpan.Zero),

            new(
                CleanupCategoryId.RecycleBin,
                "Recycle Bin",
                "Permanently deletes everything in the Recycle Bin. You cannot get these files back.",
                RequiresAdmin: false, IsSelectedByDefault: false,
                Roots: [],
                FileNamePattern: null, MinimumAge: TimeSpan.Zero),
        };

        var protectedFolders = GetProtectedFolders();
        var categories = new List<CleanupCategory>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.IsRecycleBin)
            {
                categories.Add(candidate);
                continue;
            }

            var safeRoots = candidate.Roots.Where(root => IsSafeRoot(candidate.Id, root.Path, protectedFolders)).ToList();
            if (safeRoots.Count > 0)
            {
                categories.Add(candidate with { Roots = safeRoots });
            }
        }

        return categories;
    }

    private List<string> GetProtectedFolders() =>
    [
        _paths.WindowsDirectory,
        _paths.UserProfile,
        _paths.ProgramFiles,
        _paths.ProgramFilesX86,
        _paths.ProgramData,
        _paths.LocalAppData,
    ];

    private bool IsSafeRoot(CleanupCategoryId id, string root, List<string> protectedFolders)
    {
        if (!CleanupPaths.IsDangerousRoot(root, protectedFolders))
        {
            return true;
        }

        LogRootRejected(id, root);
        return false;
    }

    private List<CleanupRoot> BuildBrowserCacheRoots(string localAppData)
    {
        var roots = new List<CleanupRoot>();
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return roots;
        }

        foreach (var (displayName, processName, relativePath) in ChromiumBrowsers)
        {
            var userData = Path.Combine([localAppData, .. relativePath]);
            foreach (var profile in ListSubdirectories(userData))
            {
                foreach (var cacheFolder in ChromiumCacheFolders)
                {
                    AddIfExists(roots, Path.Combine(profile, cacheFolder), processName, displayName);
                }
            }
        }

        foreach (var profile in ListSubdirectories(Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles")))
        {
            AddIfExists(roots, Path.Combine(profile, "cache2"), "firefox", "Firefox");
        }

        return roots;
    }

    private void AddIfExists(List<CleanupRoot> roots, string path, string processName, string displayName)
    {
        if (_fileSystem.DirectoryExists(path))
        {
            roots.Add(new CleanupRoot(path, RequiresAdmin: false, processName, displayName));
        }
    }

    /// <summary>Real (non-reparse-point) sub-directories of <paramref name="directory"/>, or none if
    /// it does not exist or cannot be read.</summary>
    private List<string> ListSubdirectories(string directory)
    {
        var result = new List<string>();
        if (!_fileSystem.DirectoryExists(directory))
        {
            return result;
        }

        try
        {
            result.AddRange(_fileSystem.EnumerateEntries(directory)
                .Where(entry => entry.IsDirectory && !entry.IsReparsePoint)
                .Select(entry => entry.FullPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A browser profile folder we can't list simply contributes no cache roots.
            LogListingFailed(ex, directory);
        }

        return result;
    }

    /// <summary>Combines a base folder with sub-folders, or returns an empty string (which is then
    /// rejected as a dangerous root) if the base folder could not be resolved.</summary>
    private static string CombineOrEmpty(string baseFolder, params string[] parts) =>
        string.IsNullOrWhiteSpace(baseFolder) ? string.Empty : Path.Combine([baseFolder, .. parts]);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected cleanup root for {Category}: {Root} is empty, relative or too broad to clean safely.")]
    private partial void LogRootRejected(CleanupCategoryId category, string root);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not list browser profile folders under {Directory}.")]
    private partial void LogListingFailed(Exception ex, string directory);
}
