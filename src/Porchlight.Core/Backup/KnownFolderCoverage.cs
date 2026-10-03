namespace Porchlight.Core.Backup;

/// <summary>Decides whether a known folder (Desktop, Documents, Pictures) lives inside OneDrive.</summary>
public static class KnownFolderCoverage
{
    /// <summary>True when <paramref name="folderPath"/> is inside one of <paramref name="oneDriveRoots"/>.
    /// Environment variables are expanded; comparison ignores case and trailing separators.</summary>
    public static bool IsInside(string? folderPath, IEnumerable<string> oneDriveRoots)
    {
        ArgumentNullException.ThrowIfNull(oneDriveRoots);
        var folder = Normalize(folderPath);
        if (folder is null)
        {
            return false;
        }

        foreach (var root in oneDriveRoots)
        {
            var normalizedRoot = Normalize(root);
            if (normalizedRoot is not null
                && folder.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A malformed registry path simply cannot be inside OneDrive; nothing to report.
            return null;
        }
    }
}
