namespace Porchlight.Core.Cleanup;

/// <summary>Path checks behind safety rules 2 and 3: whether a path is truly inside a category root,
/// and whether a would-be root is too dangerous to ever clean.</summary>
internal static class CleanupPaths
{
    /// <summary>True only if <paramref name="path"/>, after full normalisation (so <c>..</c> segments
    /// and mixed separators are resolved), is strictly inside <paramref name="root"/>: an ordinal,
    /// case-insensitive prefix match against the root plus a trailing separator. The root itself is
    /// not "inside" itself.</summary>
    public static bool IsInside(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var prefix = WithTrailingSeparator(Path.GetFullPath(root));
            var fullPath = Path.GetFullPath(path);
            return fullPath.Length > prefix.Length && fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path that cannot even be normalised is never inside anything.
            return false;
        }
    }

    /// <summary>True if <paramref name="root"/> must never be used as a cleanup root: empty,
    /// relative, a drive root, one of the protected folders, or an ancestor of one (cleaning it
    /// would reach into them).</summary>
    public static bool IsDangerousRoot(string? root, IEnumerable<string> protectedFolders)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
        {
            return true;
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }

        var pathRoot = Path.GetPathRoot(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullRoot.Length == 0 || string.Equals(fullRoot, pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var protectedFolder in protectedFolders)
        {
            if (string.IsNullOrWhiteSpace(protectedFolder) || !Path.IsPathFullyQualified(protectedFolder))
            {
                continue;
            }

            var fullProtected = Path.GetFullPath(protectedFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullRoot, fullProtected, StringComparison.OrdinalIgnoreCase) ||
                fullProtected.StartsWith(WithTrailingSeparator(fullRoot), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string WithTrailingSeparator(string fullPath) =>
        fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
}
