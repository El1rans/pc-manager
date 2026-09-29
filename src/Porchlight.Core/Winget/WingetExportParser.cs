using System.Text.Json;

namespace Porchlight.Core.Winget;

/// <summary>
/// Validates and reads a <c>winget export</c> file (schema <c>winget-packages</c>) before it is
/// handed to <c>winget import</c>, so a file that is not an export - or is absurdly large - is
/// rejected with a plain message and the user sees exactly which apps would be installed.
/// </summary>
public static class WingetExportParser
{
    /// <summary>Largest export file accepted (a real export of a few hundred apps is well under 200 KB).</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>Most apps accepted from one file.</summary>
    public const int MaxApps = 2000;

    private const int MaxIdLength = 256;
    private const int MaxJsonDepth = 16;

    public static WingetExportParseResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > MaxFileBytes)
        {
            return WingetExportParseResult.Failure("That file is too big to be an app list.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth, AllowTrailingCommas = true });

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("Sources", out var sources)
                || sources.ValueKind != JsonValueKind.Array)
            {
                return NotAnExport();
            }

            var apps = new List<WingetExportApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;

            foreach (var source in sources.EnumerateArray())
            {
                if (source.ValueKind != JsonValueKind.Object
                    || !source.TryGetProperty("Packages", out var packages)
                    || packages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var package in packages.EnumerateArray())
                {
                    if (package.ValueKind != JsonValueKind.Object
                        || !package.TryGetProperty("PackageIdentifier", out var idElement)
                        || idElement.ValueKind != JsonValueKind.String
                        || idElement.GetString() is not { } id
                        || !IsPlausibleId(id))
                    {
                        skipped++;
                        continue;
                    }

                    if (!seen.Add(id))
                    {
                        continue;
                    }

                    if (apps.Count >= MaxApps)
                    {
                        return WingetExportParseResult.Failure("That app list has too many apps to install at once.");
                    }

                    apps.Add(new WingetExportApp(id));
                }
            }

            return apps.Count == 0
                ? WingetExportParseResult.Failure("That file does not list any apps Porchlight can install.")
                : new WingetExportParseResult(apps, skipped, null);
        }
        catch (JsonException)
        {
            return NotAnExport();
        }
    }

    private static WingetExportParseResult NotAnExport() =>
        WingetExportParseResult.Failure("That file is not an app list saved by Porchlight or winget.");

    private static bool IsPlausibleId(string id) =>
        id.Length is > 0 and <= MaxIdLength && id.All(c => c > ' ' && c < 0x7F && c != '"');
}
