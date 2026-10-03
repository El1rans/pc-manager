using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <summary>
/// Reads the home page, new-tab page and default search engine of every Firefox profile from
/// <c>prefs.js</c>, <c>user.js</c> (which wins, as Firefox applies it at start-up) and
/// <c>search.json.mozlz4</c>. Strictly read-only.
/// </summary>
internal sealed partial class FirefoxSettingsReader
{
    private const long PrefsMaxBytes = 8L * 1024 * 1024;
    private const long SearchFileMaxBytes = 8L * 1024 * 1024;
    private const char HomePageSeparator = '|';

    private readonly ILogger _logger;
    private readonly ScanTally _tally;

    public FirefoxSettingsReader(ILogger logger, ScanTally tally)
    {
        _logger = logger;
        _tally = tally;
    }

    public List<ProfileSettings> Read(string profilesDirectory, CancellationToken cancellationToken)
    {
        var result = new List<ProfileSettings>();
        foreach (var profileDir in Directory.EnumerateDirectories(profilesDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetFileName(profileDir);
            var prefsPath = Path.Combine(profileDir, "prefs.js");
            if (!File.Exists(prefsPath))
            {
                continue;
            }

            try
            {
                var prefs = new Dictionary<string, string>(StringComparer.Ordinal);
                ReadPrefs(prefsPath, prefs);
                ReadPrefs(Path.Combine(profileDir, "user.js"), prefs);
                result.Add(Build(FirefoxExtensionReader.ProfileName(folder), profileDir, prefs));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read Firefox profile folder {Profile}.", folder);
                _tally.CountSkipped();
            }
        }

        return result;
    }

    /// <summary>The value of a <c>user_pref("name", value);</c> line: strings unquoted, numbers and
    /// booleans as their text.</summary>
    internal static void ParsePrefs(IEnumerable<string> lines, Dictionary<string, string> into)
    {
        foreach (var line in lines)
        {
            var match = PrefLine().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var raw = match.Groups[2].Value.Trim();
            into[match.Groups[1].Value] = raw.Length >= 2 && raw[0] == '"' ? Unquote(raw) : raw;
        }
    }

    private static string Unquote(string quoted)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(quoted) ?? string.Empty;
        }
        catch (JsonException)
        {
            // Not valid JSON escaping; fall back to the text between the quotes.
            return quoted[1..^1];
        }
    }

    private void ReadPrefs(string path, Dictionary<string, string> into)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > PrefsMaxBytes)
            {
                _logger.LogWarning("Skipping {Path}: larger than {Max} bytes.", path, PrefsMaxBytes);
                _tally.CountSkipped();
                return;
            }

            using var reader = new StreamReader(stream);
            ParsePrefs(ReadLines(reader), into);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Safe to ignore: user.js is optional.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "{Path} does not exist.", path);
            }
        }
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private ProfileSettings Build(string profileName, string profileDir, Dictionary<string, string> prefs)
    {
        var home = prefs.TryGetValue("browser.startup.homepage", out var homeValue)
            ? BrowserHijackClassifier.ClassifyAll(homeValue.Split(HomePageSeparator), forcedByPolicy: false)
            : new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.DefaultValue);

        HijackClassification? newTab = prefs.TryGetValue("browser.newtab.url", out var newTabValue)
            ? BrowserHijackClassifier.Classify(newTabValue, forcedByPolicy: false)
            : null;

        return new ProfileSettings(
            profileName,
            home,
            new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.DefaultValue),
            newTab,
            ReadSearchEngine(profileDir));
    }

    private HijackClassification ReadSearchEngine(string profileDir)
    {
        var fallback = new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.DefaultValue);
        var path = Path.Combine(profileDir, "search.json.mozlz4");
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > SearchFileMaxBytes)
            {
                _logger.LogWarning("Skipping {Path}: larger than {Max} bytes.", path, SearchFileMaxBytes);
                _tally.CountSkipped();
                return fallback;
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            using var doc = JsonDocument.Parse(MozLz4Decoder.Decode(bytes));
            return ClassifySearch(doc.RootElement);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Safe to ignore: a profile with no search file uses the browser default.
            return fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            _logger.LogWarning(ex, "Skipping unreadable or malformed search file {Path}.", path);
            _tally.CountSkipped();
            return fallback;
        }
    }

    /// <summary>The default engine named by <c>metaData</c> (<c>defaultEngineId</c>, or the older
    /// <c>current</c>), judged by its search address; an engine Firefox ships itself is fine.</summary>
    internal static HijackClassification ClassifySearch(JsonElement root)
    {
        var fallback = new HijackClassification(HijackStatus.Ok, BrowserHijackClassifier.DefaultValue);
        var meta = root.Child("metaData");
        var wanted = meta.Child("defaultEngineId").StringOf();
        if (string.IsNullOrEmpty(wanted))
        {
            wanted = meta.Child("current").StringOf();
        }

        if (string.IsNullOrEmpty(wanted) || root.Child("engines") is not { ValueKind: JsonValueKind.Array } engines)
        {
            return fallback;
        }

        foreach (var engine in engines.EnumerateArray())
        {
            var id = engine.Child("id").StringOf();
            var name = engine.Child("_name").StringOf();
            if (id != wanted && name != wanted)
            {
                continue;
            }

            var template = engine.Child("_urls") is { ValueKind: JsonValueKind.Array } urls
                ? urls.EnumerateArray().Select(u => u.Child("template").StringOf()).FirstOrDefault(t => !string.IsNullOrEmpty(t))
                : null;
            if (template is null)
            {
                return fallback;
            }

            var classified = BrowserHijackClassifier.Classify(template, forcedByPolicy: false);
            return classified.Status != HijackStatus.Ok && engine.Child("_isAppProvided").BoolOf() == true
                ? new HijackClassification(HijackStatus.Ok, name ?? classified.Value)
                : classified;
        }

        return fallback;
    }

    [GeneratedRegex("""^\s*(?:user_)?pref\(\s*"([^"]+)"\s*,\s*(.+?)\s*\)\s*;\s*$""")]
    private static partial Regex PrefLine();
}
