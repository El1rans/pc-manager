using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <summary>
/// Reads the add-ons of one Chromium-based browser (Edge, Chrome, Brave), every profile, strictly
/// read-only. See docs/specs/17-browser-extensions.md for the file layout and skip rules.
/// </summary>
internal sealed partial class ChromiumExtensionReader
{
    private const string UnknownProfileFolderSystem = "System Profile";
    private const string UnknownProfileFolderGuest = "Guest Profile";

    // Chromium ManifestLocation values.
    private const int LocationInternal = 1;
    private const int LocationComponent = 5;
    private const int LocationExternalComponent = 10;
    private const int LocationUnpacked = 4;
    private const int LocationCommandLine = 8;
    private const int LocationExternalPolicyDownload = 7;
    private const int LocationExternalPolicy = 9;

    /// <summary>Microseconds between 1601-01-01 (WebKit epoch) ticks and FILETIME: 10 ticks per us.</summary>
    private const long TicksPerMicrosecond = 10;

    private static readonly string[] FallbackLocales = ["en", "en_US"];

    private readonly ILogger _logger;
    private readonly ScanTally _tally;

    public ChromiumExtensionReader(ILogger logger, ScanTally tally)
    {
        _logger = logger;
        _tally = tally;
    }

    /// <summary>Reads every profile under <paramref name="location"/>; empty if the browser is not installed.</summary>
    public List<InstalledExtension> Read(ChromiumBrowserLocation location, CancellationToken cancellationToken)
    {
        var result = new List<InstalledExtension>();
        if (!Directory.Exists(location.UserDataDirectory))
        {
            return result;
        }

        var profileNames = ReadProfileNames(location.UserDataDirectory);
        foreach (var profileDir in Directory.EnumerateDirectories(location.UserDataDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetFileName(profileDir);
            if (folder is UnknownProfileFolderSystem or UnknownProfileFolderGuest
                || !File.Exists(Path.Combine(profileDir, "Preferences")))
            {
                continue;
            }

            var profileName = profileNames.GetValueOrDefault(folder) ?? folder;
            try
            {
                ReadProfile(location.Kind, profileDir, profileName, result, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read {Browser} profile folder {Profile}.", location.Kind, folder);
                _tally.CountSkipped();
            }
        }

        return result;
    }

    /// <summary>True if <paramref name="location"/>'s user data folder exists.</summary>
    public static bool IsInstalled(ChromiumBrowserLocation location) => Directory.Exists(location.UserDataDirectory);

    private Dictionary<string, string> ReadProfileNames(string userDataDir)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var doc = BrowserJson.TryOpen(
            Path.Combine(userDataDir, "Local State"), BrowserJson.PreferencesMaxBytes, _logger, _tally);
        if (doc is null || doc.RootElement.Child("profile").Child("info_cache") is not { ValueKind: JsonValueKind.Object } cache)
        {
            return names;
        }

        foreach (var entry in cache.EnumerateObject())
        {
            if (entry.Value.Child("name").StringOf() is { Length: > 0 } name)
            {
                names[entry.Name] = name;
            }
        }

        return names;
    }

    private void ReadProfile(
        BrowserKind browser,
        string profileDir,
        string profileName,
        List<InstalledExtension> result,
        CancellationToken cancellationToken)
    {
        using var secure = BrowserJson.TryOpen(
            Path.Combine(profileDir, "Secure Preferences"), BrowserJson.PreferencesMaxBytes, _logger, _tally);
        using var prefs = BrowserJson.TryOpen(
            Path.Combine(profileDir, "Preferences"), BrowserJson.PreferencesMaxBytes, _logger, _tally);

        // id -> the settings entries for it (Secure Preferences first, which is authoritative).
        var entries = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        foreach (var doc in new[] { secure, prefs })
        {
            if (doc?.RootElement.Child("extensions").Child("settings") is not { ValueKind: JsonValueKind.Object } settings)
            {
                continue;
            }

            foreach (var entry in settings.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!entries.TryGetValue(entry.Name, out var list))
                {
                    entries[entry.Name] = list = [];
                }

                list.Add(entry.Value);
            }
        }

        var extensionsDir = Path.Combine(profileDir, "Extensions");
        foreach (var (id, candidates) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ExtensionIdPattern().IsMatch(id))
            {
                continue;
            }

            try
            {
                if (ReadExtension(browser, profileName, id, candidates, extensionsDir) is { } extension)
                {
                    result.Add(extension);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Skipping {Browser} extension {Id}: unreadable.", browser, id);
                _tally.CountSkipped();
            }
        }
    }

    private InstalledExtension? ReadExtension(
        BrowserKind browser,
        string profileName,
        string id,
        List<JsonElement> candidates,
        string extensionsDir)
    {
        JsonElement? Get(string name) =>
            candidates.Select(c => c.Child(name)).FirstOrDefault(v => v is not null);

        var location = (int)(Get("location").LongOf() ?? 0);
        if (location is LocationComponent or LocationExternalComponent)
        {
            return null;
        }

        if (Get("was_installed_by_default").BoolOf() == true)
        {
            return null;
        }

        var manifestDir = FindManifestDirectory(extensionsDir, id, Get("path").StringOf());
        JsonDocument? manifestDoc = null;
        try
        {
            if (manifestDir is not null)
            {
                manifestDoc = BrowserJson.TryOpen(
                    Path.Combine(manifestDir, "manifest.json"), BrowserJson.ManifestMaxBytes, _logger, _tally);
            }

            JsonElement? manifest = manifestDoc?.RootElement ?? Get("manifest");
            if (manifest is not { ValueKind: JsonValueKind.Object } m)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Skipping {Browser} extension {Id}: no manifest found.", browser, id);
                }

                return null;
            }

            var defaultLocale = m.Child("default_locale").StringOf();
            var name = ResolveMessage(m.Child("name").StringOf(), manifestDir, defaultLocale);
            var description = ResolveMessage(m.Child("description").StringOf(), manifestDir, defaultLocale);

            var permissions = new List<string>();
            var hosts = new List<string>();
            foreach (var permission in m.Child("permissions").Strings())
            {
                (LooksLikeHostPattern(permission) ? hosts : permissions).Add(permission);
            }

            hosts.AddRange(m.Child("host_permissions").Strings());
            if (m.Child("content_scripts") is { ValueKind: JsonValueKind.Array } scripts)
            {
                foreach (var script in scripts.EnumerateArray())
                {
                    hosts.AddRange(script.Child("matches").Strings());
                }
            }

            var disabled = Get("state").LongOf() == 0 || (Get("disable_reasons").LongOf() ?? 0) != 0;
            return new InstalledExtension(
                browser,
                profileName,
                id,
                string.IsNullOrWhiteSpace(name) ? id : name,
                description ?? string.Empty,
                m.Child("version").StringOf() ?? string.Empty,
                !disabled,
                ToInstallTime(Get("install_time").LongOf()),
                ToSource(location, Get("from_webstore").BoolOf()),
                permissions,
                hosts);
        }
        finally
        {
            manifestDoc?.Dispose();
        }
    }

    private static bool LooksLikeHostPattern(string permission) =>
        permission.Contains("://", StringComparison.Ordinal) || permission == "<all_urls>";

    private static ExtensionSource ToSource(int location, bool? fromWebStore) => location switch
    {
        LocationInternal => fromWebStore == true ? ExtensionSource.Store : ExtensionSource.Unknown,
        2 or 3 or 6 => ExtensionSource.Sideloaded,
        LocationExternalPolicyDownload or LocationExternalPolicy => ExtensionSource.Policy,
        LocationUnpacked or LocationCommandLine => ExtensionSource.Developer,
        _ => ExtensionSource.Unknown,
    };

    private static DateTimeOffset? ToInstallTime(long? webKitMicroseconds)
    {
        if (webKitMicroseconds is not > 0)
        {
            return null;
        }

        var fileTime = webKitMicroseconds.Value * TicksPerMicrosecond;
        if (webKitMicroseconds.Value > long.MaxValue / TicksPerMicrosecond
            || fileTime > DateTime.MaxValue.ToFileTimeUtc())
        {
            return null;
        }

        return new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime), TimeSpan.Zero);
    }

    /// <summary>The folder holding manifest.json: the entry's own path (relative to Extensions, or
    /// absolute for unpacked ones) if it has one, else the newest version folder for the id.</summary>
    private static string? FindManifestDirectory(string extensionsDir, string id, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            string full;
            try
            {
                full = Path.GetFullPath(path, extensionsDir);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                full = string.Empty;
            }

            var isInsideExtensions = full.StartsWith(
                Path.GetFullPath(extensionsDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (full.Length > 0 && (Path.IsPathRooted(path) || isInsideExtensions) && File.Exists(Path.Combine(full, "manifest.json")))
            {
                return full;
            }
        }

        var idDir = Path.Combine(extensionsDir, id);
        if (!Directory.Exists(idDir))
        {
            return null;
        }

        return Directory.EnumerateDirectories(idDir)
            .Where(d => File.Exists(Path.Combine(d, "manifest.json")))
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Resolves a <c>__MSG_key__</c> placeholder through <c>_locales</c>; other text is returned as is.</summary>
    private string? ResolveMessage(string? value, string? manifestDir, string? defaultLocale)
    {
        if (value is null)
        {
            return null;
        }

        var match = MessagePlaceholderPattern().Match(value);
        if (!match.Success || manifestDir is null)
        {
            return match.Success ? null : value;
        }

        var key = match.Groups[1].Value;
        var locales = new List<string>();
        if (!string.IsNullOrEmpty(defaultLocale))
        {
            locales.Add(defaultLocale);
        }

        locales.AddRange(FallbackLocales);
        foreach (var locale in locales.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (locale.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
                || locale.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            using var doc = BrowserJson.TryOpen(
                Path.Combine(manifestDir, "_locales", locale, "messages.json"),
                BrowserJson.ManifestMaxBytes,
                _logger,
                _tally);
            if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase)
                    && property.Value.Child("message").StringOf() is { } message)
                {
                    return message;
                }
            }
        }

        return null;
    }

    [GeneratedRegex("^[a-p]{32}$")]
    private static partial Regex ExtensionIdPattern();

    [GeneratedRegex("^__MSG_(.+)__$")]
    private static partial Regex MessagePlaceholderPattern();
}
