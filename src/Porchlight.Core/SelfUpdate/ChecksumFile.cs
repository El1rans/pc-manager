using System.Text.RegularExpressions;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Reads the <c>.sha256</c> file the release workflow publishes next to the installer:
/// <c>&lt;64 hex digits&gt; *Porchlight-Setup-X.Y.Z.exe</c> (lower-case hex, one line, no trailing newline -
/// see the "Compute SHA-256 checksum" step in <c>.github/workflows/release.yml</c>).</summary>
public static partial class ChecksumFile
{
    [GeneratedRegex(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();

    /// <summary>Extracts the expected SHA-256 (as lower-case hex) for <paramref name="expectedFileName"/>.
    /// Returns false for anything that isn't exactly one hash optionally followed by that file's name
    /// (with the <c>*</c> binary-mode marker or a leading space) - a checksum for some other file is
    /// rejected rather than trusted.</summary>
    public static bool TryParse(string? content, string expectedFileName, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(content) || content.Length > 4096)
        {
            return false;
        }

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 1)
        {
            return false;
        }

        var parts = lines[0].Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !HexPattern().IsMatch(parts[0]))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            var name = parts[1].TrimStart('*').Trim();
            if (!string.Equals(name, expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        hash = parts[0].ToLowerInvariant();
        return true;
    }
}
