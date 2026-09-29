using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>The outcome of <see cref="CustomAnimationParser.Parse"/>: exactly one of
/// <see cref="Animation"/> or <see cref="Error"/> is set.</summary>
public sealed record CustomAnimationParseResult(CustomAnimation? Animation, string? Error)
{
    public bool Succeeded => Animation is not null;
}

/// <summary>
/// Parses and validates a <c>porchlight-animation</c> JSON document (see
/// docs/custom-animations.md) into a <see cref="CustomAnimation"/>. Porchlight runs elevated and
/// these files come from the internet or an AI chat, so parsing is strict about size (every limit
/// below is enforced before anything is allocated for it) and never throws for bad input - it
/// returns a plain-language error naming exactly what's wrong and where (e.g. "Frame 3, row 2:
/// 'X' is not in the palette."), short enough to paste straight back into the AI chat that wrote
/// the file.
/// </summary>
public static class CustomAnimationParser
{
    public const string FormatId = "porchlight-animation";
    public const int CurrentVersion = 1;

    /// <summary>Largest accepted document, in characters (and the byte limit
    /// <see cref="CustomAnimationLibrary"/> checks before reading a file).</summary>
    public const int MaxDocumentLength = 512 * 1024;

    public const int MaxFrames = 1000;

    /// <summary>Largest grid width or height. The biggest real matrix Porchlight targets today, a
    /// full-size keyboard, is about 27x7.</summary>
    public const int MaxGridSize = 64;

    public const int MaxPaletteEntries = 64;
    public const int MaxNameLength = 60;
    public const int MaxTextLength = 500;
    public const double MinFrameSeconds = 0.02;
    public const double MaxFrameSeconds = 600;
    public const double DefaultFrameSeconds = 0.1;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 16,
    };

    public static CustomAnimationParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("The animation is empty.");
        }

        if (json.Length > MaxDocumentLength)
        {
            return Fail($"The animation is too large (the limit is {MaxDocumentLength / 1024} KB).");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(StripCodeFence(json), DocumentOptions);
        }
        catch (JsonException ex)
        {
            return Fail($"This isn't valid JSON ({ex.Message}).");
        }

        using (document)
        {
            try
            {
                return new CustomAnimationParseResult(ParseRoot(document.RootElement), null);
            }
            catch (FormatException ex)
            {
                return Fail(ex.Message);
            }
        }
    }

    /// <summary>AI chats usually wrap JSON in a Markdown code fence (<c>```json ... ```</c>); accept
    /// a pasted answer as-is rather than making the user trim it by hand.</summary>
    private static string StripCodeFence(string json)
    {
        var text = json.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n', StringComparison.Ordinal);
        if (firstNewline < 0)
        {
            return text;
        }

        text = text[(firstNewline + 1)..].TrimEnd();
        return text.EndsWith("```", StringComparison.Ordinal) ? text[..^3] : text;
    }

    private static CustomAnimation ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The animation must be a JSON object ({ ... }).");
        }

        if (root.TryGetProperty("format", out var format) &&
            !(format.ValueKind == JsonValueKind.String && format.GetString() == FormatId))
        {
            throw new FormatException($"\"format\" must be \"{FormatId}\".");
        }

        if (root.TryGetProperty("version", out var version) &&
            !(version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == CurrentVersion))
        {
            throw new FormatException($"\"version\" must be {CurrentVersion}.");
        }

        var name = ReadText(root, "name", MaxNameLength)
            ?? throw new FormatException("\"name\" is required (a short name for the animation).");
        var description = ReadText(root, "description", MaxTextLength);
        var author = ReadText(root, "author", MaxNameLength);

        var loop = true;
        if (root.TryGetProperty("loop", out var loopElement))
        {
            loop = loopElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new FormatException("\"loop\" must be true or false."),
            };
        }

        var transition = CustomAnimationTransition.Cut;
        if (root.TryGetProperty("transition", out var transitionElement))
        {
            transition = (transitionElement.ValueKind == JsonValueKind.String ? transitionElement.GetString() : null) switch
            {
                "cut" => CustomAnimationTransition.Cut,
                "fade" => CustomAnimationTransition.Fade,
                _ => throw new FormatException("\"transition\" must be \"cut\" or \"fade\"."),
            };
        }

        var defaultDuration = ReadDuration(root, "frameDuration", DefaultFrameSeconds, "\"frameDuration\"");
        var palette = ReadPalette(root);

        if (!root.TryGetProperty("frames", out var framesElement) || framesElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("\"frames\" is required and must be a list of frames.");
        }

        var frameCount = framesElement.GetArrayLength();
        if (frameCount is 0 or > MaxFrames)
        {
            throw new FormatException($"\"frames\" must have between 1 and {MaxFrames} frames (it has {frameCount}).");
        }

        var frames = new List<CustomAnimationFrame>(frameCount);
        var number = 1;
        foreach (var frameElement in framesElement.EnumerateArray())
        {
            frames.Add(ParseFrame(frameElement, number, palette, defaultDuration));
            number++;
        }

        return new CustomAnimation(name, description, author, loop, transition, frames);
    }

    private static string? ReadText(JsonElement root, string property, int maxLength)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"\"{property}\" must be text.");
        }

        // Control characters (newlines, tabs, ...) are dropped: every one of these is shown on a
        // single line in the UI.
        var builder = new StringBuilder();
        foreach (var c in element.GetString()!)
        {
            if (!char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        var text = builder.ToString().Trim();
        if (text.Length > maxLength)
        {
            throw new FormatException($"\"{property}\" is too long (at most {maxLength} characters).");
        }

        return text.Length == 0 ? null : text;
    }

    private static TimeSpan ReadDuration(JsonElement parent, string property, double fallbackSeconds, string where)
    {
        if (!parent.TryGetProperty(property, out var element))
        {
            return TimeSpan.FromSeconds(fallbackSeconds);
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var seconds) ||
            !double.IsFinite(seconds) || seconds < MinFrameSeconds || seconds > MaxFrameSeconds)
        {
            throw new FormatException(string.Create(
                CultureInfo.InvariantCulture,
                $"{where} must be a number of seconds between {MinFrameSeconds} and {MaxFrameSeconds}."));
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static Dictionary<char, RgbColor> ReadPalette(JsonElement root)
    {
        var palette = new Dictionary<char, RgbColor>();
        if (!root.TryGetProperty("palette", out var element))
        {
            return palette;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("\"palette\" must be an object mapping single characters to \"#RRGGBB\" colors.");
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (palette.Count >= MaxPaletteEntries)
            {
                throw new FormatException($"\"palette\" has too many entries (at most {MaxPaletteEntries}).");
            }

            if (entry.Name.Length != 1)
            {
                throw new FormatException($"Palette key \"{Shorten(entry.Name)}\" must be exactly one character.");
            }

            if (entry.Value.ValueKind != JsonValueKind.String || !RgbColor.TryParse(entry.Value.GetString(), out var color))
            {
                throw new FormatException($"Palette entry \"{entry.Name}\" must be a \"#RRGGBB\" color.");
            }

            palette[entry.Name[0]] = color;
        }

        return palette;
    }

    private static CustomAnimationFrame ParseFrame(
        JsonElement element, int number, Dictionary<char, RgbColor> palette, TimeSpan defaultDuration)
    {
        var where = $"Frame {number}";
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"{where} must be an object with \"fill\", \"gradient\" or \"rows\".");
        }

        var hasFill = element.TryGetProperty("fill", out var fill);
        var hasGradient = element.TryGetProperty("gradient", out var gradient);
        var hasRows = element.TryGetProperty("rows", out var rows);
        if ((hasFill ? 1 : 0) + (hasGradient ? 1 : 0) + (hasRows ? 1 : 0) != 1)
        {
            throw new FormatException($"{where} must have exactly one of \"fill\", \"gradient\" or \"rows\".");
        }

        var duration = element.TryGetProperty("duration", out _)
            ? ReadDuration(element, "duration", DefaultFrameSeconds, $"{where}: \"duration\"")
            : defaultDuration;

        if (hasFill)
        {
            var color = ResolveColor(fill, palette, $"{where}: \"fill\"");
            return new CustomAnimationFrame(1, 1, [color], isGradient: false, duration);
        }

        if (hasGradient)
        {
            if (gradient.ValueKind != JsonValueKind.Array || gradient.GetArrayLength() is < 2 or > MaxGridSize)
            {
                throw new FormatException($"{where}: \"gradient\" must be a list of 2 to {MaxGridSize} colors.");
            }

            var stops = new RgbColor[gradient.GetArrayLength()];
            var i = 0;
            foreach (var stop in gradient.EnumerateArray())
            {
                stops[i] = ResolveColor(stop, palette, $"{where}: gradient color {i + 1}");
                i++;
            }

            return new CustomAnimationFrame(stops.Length, 1, stops, isGradient: true, duration);
        }

        return ParseRows(rows, where, palette, duration);
    }

    private static CustomAnimationFrame ParseRows(
        JsonElement rows, string where, Dictionary<char, RgbColor> palette, TimeSpan duration)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() is 0 or > MaxGridSize)
        {
            throw new FormatException($"{where}: \"rows\" must be a list of 1 to {MaxGridSize} text rows.");
        }

        if (palette.Count == 0)
        {
            throw new FormatException($"{where} uses \"rows\", which needs a \"palette\" saying what color each character is.");
        }

        var height = rows.GetArrayLength();
        var width = -1;
        RgbColor[]? pixels = null;
        var rowNumber = 0;
        foreach (var rowElement in rows.EnumerateArray())
        {
            rowNumber++;
            var text = rowElement.ValueKind == JsonValueKind.String ? rowElement.GetString()! : null;
            if (text is null || text.Length is 0 or > MaxGridSize)
            {
                throw new FormatException($"{where}, row {rowNumber} must be text of 1 to {MaxGridSize} characters.");
            }

            if (width < 0)
            {
                width = text.Length;
                pixels = new RgbColor[width * height];
            }
            else if (text.Length != width)
            {
                throw new FormatException(
                    $"{where}, row {rowNumber} is {text.Length} characters long but row 1 is {width} - every row must be the same length.");
            }

            for (var col = 0; col < width; col++)
            {
                if (!palette.TryGetValue(text[col], out var color))
                {
                    throw new FormatException($"{where}, row {rowNumber}: '{text[col]}' is not in the palette.");
                }

                pixels![((rowNumber - 1) * width) + col] = color;
            }
        }

        return new CustomAnimationFrame(width, height, pixels!, isGradient: false, duration);
    }

    private static RgbColor ResolveColor(JsonElement element, Dictionary<char, RgbColor> palette, string where)
    {
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        if (text is { Length: 1 } && palette.TryGetValue(text[0], out var fromPalette))
        {
            return fromPalette;
        }

        if (RgbColor.TryParse(text, out var color))
        {
            return color;
        }

        throw new FormatException($"{where} must be a \"#RRGGBB\" color or a palette character.");
    }

    private static string Shorten(string text) => text.Length <= 20 ? text : text[..20] + "...";

    private static CustomAnimationParseResult Fail(string error) => new(null, error);
}
