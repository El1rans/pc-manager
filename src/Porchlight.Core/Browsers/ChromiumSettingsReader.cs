using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <summary>
/// Reads the home page, startup pages and default search engine of every profile of one
/// Chromium-based browser from <c>Secure Preferences</c> / <c>Preferences</c>, strictly read-only.
/// A setting present in both files is taken from <c>Secure Preferences</c> (the protected copy).
/// </summary>
internal sealed class ChromiumSettingsReader
{
    private const string SystemProfileFolder = "System Profile";
    private const string GuestProfileFolder = "Guest Profile";

    // session.restore_on_startup values.
    private const int RestoreLastSession = 1;
    private const int RestoreSpecificPages = 4;
    private const int RestoreNewTabPage = 5;

    public const string RestoreLastSessionValue = "Reopens the tabs you had open";

    private readonly ILogger _logger;
    private readonly ScanTally _tally;

    public ChromiumSettingsReader(ILogger logger, ScanTally tally)
    {
        _logger = logger;
        _tally = tally;
    }

    public List<ProfileSettings> Read(ChromiumBrowserLocation location, CancellationToken cancellationToken)
    {
        var result = new List<ProfileSettings>();
        if (!Directory.Exists(location.UserDataDirectory))
        {
            return result;
        }

        var names = ChromiumExtensionReader.ReadProfileNames(location.UserDataDirectory, _logger, _tally);
        foreach (var profileDir in Directory.EnumerateDirectories(location.UserDataDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetFileName(profileDir);
            if (folder is SystemProfileFolder or GuestProfileFolder
                || !File.Exists(Path.Combine(profileDir, "Preferences")))
            {
                continue;
            }

            try
            {
                result.Add(ReadProfile(profileDir, names.GetValueOrDefault(folder) ?? folder));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read {Browser} profile folder {Profile}.", location.Kind, folder);
                _tally.CountSkipped();
            }
        }

        return result;
    }

    private ProfileSettings ReadProfile(string profileDir, string profileName)
    {
        using var secure = BrowserJson.TryOpen(
            Path.Combine(profileDir, "Secure Preferences"), BrowserJson.PreferencesMaxBytes, _logger, _tally);
        using var prefs = BrowserJson.TryOpen(
            Path.Combine(profileDir, "Preferences"), BrowserJson.PreferencesMaxBytes, _logger, _tally);
        JsonDocument?[] docs = [secure, prefs];

        var homeIsNewTab = FirstValue(docs, root => root.Child("homepage_is_newtabpage").BoolOf());
        var homePage = homeIsNewTab == true
            ? new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.OwnPageValue)
            : BrowserHijackClassifier.Classify(First(docs, root => root.Child("homepage").StringOf()), forcedByPolicy: false);

        var restore = FirstValue(docs, root => root.Child("session").Child("restore_on_startup").LongOf());
        var startup = restore switch
        {
            RestoreSpecificPages => BrowserHijackClassifier.ClassifyAll(
                First(docs, root => Listed(root.Child("session").Child("startup_urls"))) ?? [], forcedByPolicy: false),
            RestoreNewTabPage => new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.OwnPageValue),
            RestoreLastSession => new HijackClassification(HijackStatus.Ok, RestoreLastSessionValue),
            _ => new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.DefaultValue),
        };

        var search = BrowserHijackClassifier.Classify(
            First(docs, root => root.Child("default_search_provider_data").Child("template_url_data").Child("url").StringOf()),
            forcedByPolicy: false);

        return new ProfileSettings(profileName, homePage, startup, null, search);
    }

    private static List<string>? Listed(JsonElement? element)
    {
        var list = element.Strings().ToList();
        return list.Count == 0 ? null : list;
    }

    private static T? FirstValue<T>(JsonDocument?[] docs, Func<JsonElement, T?> read)
        where T : struct
    {
        foreach (var doc in docs)
        {
            if (doc is not null && read(doc.RootElement) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static T? First<T>(JsonDocument?[] docs, Func<JsonElement, T?> read)
        where T : class
    {
        foreach (var doc in docs)
        {
            if (doc is not null && read(doc.RootElement) is { } value)
            {
                return value;
            }
        }

        return null;
    }
}
