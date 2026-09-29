using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <summary>
/// Read-only, size-capped JSON access to browser files. Browsers keep these files open, so they are
/// opened with <c>FileShare.ReadWrite | FileShare.Delete</c>; nothing here ever writes.
/// </summary>
internal static class BrowserJson
{
    public const long PreferencesMaxBytes = 32L * 1024 * 1024;
    public const long ExtensionsJsonMaxBytes = 16L * 1024 * 1024;
    public const long ManifestMaxBytes = 2L * 1024 * 1024;

    /// <summary>Cap on how many permission strings are read from one add-on.</summary>
    public const int MaxPermissionsPerExtension = 500;

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 64,
    };

    /// <summary>
    /// Parses <paramref name="path"/>. Returns null when the file does not exist (not counted) or
    /// cannot be used (too large, unreadable or malformed: logged and counted as skipped).
    /// </summary>
    public static JsonDocument? TryOpen(string path, long maxBytes, ILogger logger, ScanTally tally)
    {
        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maxBytes)
            {
                logger.LogWarning("Skipping {Path}: larger than {Max} bytes.", path, maxBytes);
                tally.CountSkipped();
                return null;
            }

            return JsonDocument.Parse(stream, ReadOptions);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Safe to ignore: an absent optional file simply means there is nothing to read.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "{Path} does not exist.", path);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Skipping unreadable or malformed file {Path}.", path);
            tally.CountSkipped();
            return null;
        }
    }

    public static JsonElement? Child(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    public static JsonElement? Child(this JsonElement? element, string name) =>
        element is { } e ? e.Child(name) : null;

    public static string? StringOf(this JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } e ? e.GetString() : null;

    public static bool? BoolOf(this JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null,
    };

    /// <summary>Integer value; also accepts a number stored as a string (Chromium does this for
    /// timestamps).</summary>
    public static long? LongOf(this JsonElement? element)
    {
        if (element is { ValueKind: JsonValueKind.Number } n && n.TryGetInt64(out var value))
        {
            return value;
        }

        if (element is { ValueKind: JsonValueKind.String } s
            && long.TryParse(s.GetString(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>The string items of an array (other item types ignored), capped at
    /// <see cref="MaxPermissionsPerExtension"/>.</summary>
    public static IEnumerable<string> Strings(this JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            yield break;
        }

        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
            {
                yield return text;
                if (++count >= MaxPermissionsPerExtension)
                {
                    yield break;
                }
            }
        }
    }
}
