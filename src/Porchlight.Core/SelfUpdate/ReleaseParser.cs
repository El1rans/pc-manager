using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Pure parsing/comparison helpers for the GitHub "latest release" JSON and version tags.</summary>
public static partial class ReleaseParser
{
    /// <summary>The repository releases are read from (see <c>docs/RELEASING.md</c>).</summary>
    public const string Repository = "El1rans/porchlight";

    [GeneratedRegex(@"^[vV]?(\d{1,5})\.(\d{1,5})\.(\d{1,5})$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    /// <summary>The installer asset name the release workflow publishes for a version.</summary>
    public static string InstallerFileNameFor(Version version) =>
        string.Create(CultureInfo.InvariantCulture, $"Porchlight-Setup-{version.Major}.{version.Minor}.{version.Build}.exe");

    /// <summary>Parses a strict <c>vX.Y.Z</c> tag (the leading "v" is optional). Anything else - a
    /// pre-release suffix, a fourth component, text - is malformed and returns false.</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var match = TagPattern().Match(tag.Trim());
        if (!match.Success)
        {
            return false;
        }

        version = new Version(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>Parses the running app's version text (e.g. <c>0.1.0</c>, <c>0.1.0-beta+abc123</c>),
    /// dropping any pre-release/build suffix. Returns false if it isn't a version at all.</summary>
    public static bool TryParseAppVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var end = text.IndexOfAny(['+', '-']);
        var core = end < 0 ? text : text[..end];
        if (!Version.TryParse(core.Trim(), out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }

    /// <summary>True when <paramref name="candidate"/> is strictly newer than <paramref name="current"/>
    /// using <see cref="Version"/> ordering. Missing components count as 0, so <c>1.2</c> equals
    /// <c>1.2.0</c> (plain <see cref="Version"/> comparison would call 1.2 older than 1.2.0).</summary>
    public static bool IsNewer(Version candidate, Version current) =>
        Normalize(candidate).CompareTo(Normalize(current)) > 0;

    private static Version Normalize(Version v) =>
        new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>
    /// Reads a release from the GitHub API's JSON. Returns null when the JSON is malformed, the tag
    /// is not a strict <c>vX.Y.Z</c>, or the release is a draft or pre-release (the "latest" endpoint
    /// never returns those; this is defence in depth). A release without the expected assets is still
    /// returned, with null download URLs, so the user can at least be pointed at its page.
    /// </summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (GetBool(root, "draft") || GetBool(root, "prerelease"))
            {
                return null;
            }

            var tag = GetString(root, "tag_name");
            if (!TryParseTag(tag, out var version))
            {
                return null;
            }

            var trimmedTag = tag!.Trim();
            var page = ParsePageUrl(GetString(root, "html_url"), trimmedTag);
            var notes = GetString(root, "body");

            Uri? installer = null;
            Uri? checksum = null;
            var installerName = InstallerFileNameFor(version);
            var checksumName = installerName + ".sha256";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = GetString(asset, "name");
                    var url = GetString(asset, "browser_download_url");
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        continue;
                    }

                    if (string.Equals(name, installerName, StringComparison.OrdinalIgnoreCase))
                    {
                        installer = uri;
                    }
                    else if (string.Equals(name, checksumName, StringComparison.OrdinalIgnoreCase))
                    {
                        checksum = uri;
                    }
                }
            }

            return new ReleaseInfo(version, trimmedTag, page, string.IsNullOrWhiteSpace(notes) ? null : notes, installer, checksum);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri ParsePageUrl(string? htmlUrl, string tag)
    {
        // Only ever hand a github.com https URL to the browser launcher; anything else falls back
        // to the tag page built from the known repository.
        if (Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri) && SelfUpdateUrlPolicy.IsGitHubHttps(uri))
        {
            return uri;
        }

        return new Uri($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}");
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
