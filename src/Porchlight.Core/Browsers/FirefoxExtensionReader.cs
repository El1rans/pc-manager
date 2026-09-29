using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <summary>Reads Firefox add-ons from each profile's <c>extensions.json</c>, strictly read-only.</summary>
internal sealed class FirefoxExtensionReader
{
    private const string ExtensionType = "extension";
    private const int SignedStateSigned = 2;

    private readonly ILogger _logger;
    private readonly ScanTally _tally;

    public FirefoxExtensionReader(ILogger logger, ScanTally tally)
    {
        _logger = logger;
        _tally = tally;
    }

    public static bool IsInstalled(string? profilesDirectory) =>
        !string.IsNullOrEmpty(profilesDirectory) && Directory.Exists(profilesDirectory);

    public List<InstalledExtension> Read(string profilesDirectory, CancellationToken cancellationToken)
    {
        var result = new List<InstalledExtension>();
        foreach (var profileDir in Directory.EnumerateDirectories(profilesDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetFileName(profileDir);
            using var doc = BrowserJson.TryOpen(
                Path.Combine(profileDir, "extensions.json"), BrowserJson.ExtensionsJsonMaxBytes, _logger, _tally);
            if (doc?.RootElement.Child("addons") is not { ValueKind: JsonValueKind.Array } addons)
            {
                continue;
            }

            var profileName = ProfileName(folder);
            foreach (var addon in addons.EnumerateArray())
            {
                try
                {
                    if (ReadAddon(profileName, addon) is { } extension)
                    {
                        result.Add(extension);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException)
                {
                    _logger.LogWarning(ex, "Skipping a Firefox add-on in profile {Profile}: unreadable.", folder);
                    _tally.CountSkipped();
                }
            }
        }

        return result;
    }

    /// <summary>Firefox profile folders are "randomtext.name"; the friendly name is the part after the first dot.</summary>
    internal static string ProfileName(string folder)
    {
        var dot = folder.IndexOf('.', StringComparison.Ordinal);
        return dot >= 0 && dot < folder.Length - 1 ? folder[(dot + 1)..] : folder;
    }

    private static InstalledExtension? ReadAddon(string profileName, JsonElement addon)
    {
        if (addon.ValueKind != JsonValueKind.Object
            || addon.Child("type").StringOf() != ExtensionType
            || addon.Child("hidden").BoolOf() == true
            || addon.Child("id").StringOf() is not { Length: > 0 } id)
        {
            return null;
        }

        var location = addon.Child("location").StringOf() ?? string.Empty;
        if (IsBuiltInLocation(location))
        {
            return null;
        }

        var locale = addon.Child("defaultLocale");
        var name = locale.Child("name").StringOf();
        return new InstalledExtension(
            BrowserKind.Firefox,
            profileName,
            id,
            string.IsNullOrWhiteSpace(name) ? id : name,
            locale.Child("description").StringOf() ?? string.Empty,
            addon.Child("version").StringOf() ?? string.Empty,
            addon.Child("active").BoolOf() ?? false,
            ToInstallTime(addon.Child("installDate").LongOf()),
            ToSource(addon, location),
            addon.Child("userPermissions").Child("permissions").Strings().ToList(),
            addon.Child("userPermissions").Child("origins").Strings().ToList());
    }

    private static bool IsBuiltInLocation(string location) =>
        location.StartsWith("app-builtin", StringComparison.Ordinal)
        || location is "app-system-addons" or "app-system-defaults";

    private static ExtensionSource ToSource(JsonElement addon, string location)
    {
        if (location == "app-temporary")
        {
            return ExtensionSource.Developer;
        }

        if (addon.Child("installTelemetryInfo").Child("source").StringOf() == "enterprise-policy")
        {
            return ExtensionSource.Policy;
        }

        if (location is "app-global" or "app-system-share" or "app-system-local" or "app-system-user")
        {
            return ExtensionSource.Sideloaded;
        }

        return addon.Child("signedState").LongOf() == SignedStateSigned
            ? ExtensionSource.Store
            : ExtensionSource.Sideloaded;
    }

    private static DateTimeOffset? ToInstallTime(long? unixMilliseconds)
    {
        if (unixMilliseconds is not > 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Safe to ignore: an out-of-range date just means "installed date unknown".
            return null;
        }
    }
}
