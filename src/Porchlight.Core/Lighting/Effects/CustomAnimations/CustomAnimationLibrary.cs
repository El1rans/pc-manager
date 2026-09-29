using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <inheritdoc cref="ICustomAnimationLibrary"/>
/// <remarks>Files live in <c>%APPDATA%\Porchlight\Animations</c>, one <c>&lt;id&gt;.json</c> per
/// animation. Ids are always re-validated (<see cref="IsValidId"/>) before touching the disk, since
/// they also come back from the user-editable settings file and Porchlight runs elevated - a
/// crafted id can never reach outside that folder.</remarks>
public sealed class CustomAnimationLibrary : ICustomAnimationLibrary
{
    private const int MaxIdLength = 40;
    private const string FileExtension = ".json";

    private readonly string _folder;
    private readonly ILogger<CustomAnimationLibrary> _logger;
    private readonly Lock _gate = new();

    public CustomAnimationLibrary(ILogger<CustomAnimationLibrary> logger)
        : this(DefaultFolder(), logger)
    {
    }

    /// <summary>Test seam: stores animations in <paramref name="folder"/> instead of app data.</summary>
    internal CustomAnimationLibrary(string folder, ILogger<CustomAnimationLibrary> logger)
    {
        _folder = folder;
        _logger = logger;
    }

    public IReadOnlyList<CustomAnimationInfo> List()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_folder))
            {
                return [];
            }

            var results = new List<CustomAnimationInfo>();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(_folder, "*" + FileExtension).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not list custom animations.");
                return [];
            }

            foreach (var file in files)
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (IsValidId(id) && LoadCore(id) is { } animation)
                {
                    results.Add(ToInfo(id, animation));
                }
            }

            return [.. results.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    public CustomAnimation? Load(string id)
    {
        lock (_gate)
        {
            return IsValidId(id) ? LoadCore(id) : null;
        }
    }

    public CustomAnimationImportResult ImportFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string text;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return new CustomAnimationImportResult(null, "That file doesn't exist.");
            }

            if (info.Length > CustomAnimationParser.MaxDocumentLength)
            {
                return new CustomAnimationImportResult(
                    null, $"That file is too large (the limit is {CustomAnimationParser.MaxDocumentLength / 1024} KB).");
            }

            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Could not read custom animation file.");
            return new CustomAnimationImportResult(null, "Couldn't read that file.");
        }

        return ImportText(text);
    }

    public CustomAnimationImportResult ImportText(string json)
    {
        var parsed = CustomAnimationParser.Parse(json);
        if (parsed.Animation is not { } animation)
        {
            return new CustomAnimationImportResult(null, parsed.Error);
        }

        var id = IdFor(animation.Name);
        lock (_gate)
        {
            var target = PathFor(id);
            var temp = target + ".tmp";
            try
            {
                Directory.CreateDirectory(_folder);
                File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temp, target, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not save custom animation {Id}.", id);
                TryDelete(temp);
                return new CustomAnimationImportResult(null, "Couldn't save the animation. Please try again.");
            }
        }

        return new CustomAnimationImportResult(ToInfo(id, animation), null);
    }

    public bool Remove(string id)
    {
        if (!IsValidId(id))
        {
            return false;
        }

        lock (_gate)
        {
            var path = PathFor(id);
            return File.Exists(path) && TryDelete(path);
        }
    }

    /// <summary>Whether <paramref name="id"/> is a well-formed animation id: 1-40 characters of
    /// lowercase ASCII letters, digits and single dashes, not starting or ending with a dash.</summary>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength || id[0] == '-' || id[^1] == '-')
        {
            return false;
        }

        for (var i = 0; i < id.Length; i++)
        {
            var c = id[i];
            var ok = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') || (c == '-' && id[i - 1] != '-');
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The id an animation named <paramref name="name"/> is stored under, e.g.
    /// "Police Lights!" -> "police-lights". Always passes <see cref="IsValidId"/>.</summary>
    public static string IdFor(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (builder.Length >= MaxIdLength)
            {
                break;
            }

            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var id = builder.ToString().Trim('-');
        if (id.Length > 0)
        {
            return id;
        }

        // A name with no ASCII letters or digits at all (e.g. written entirely in Hebrew): a short,
        // stable hash keeps different names from all landing on the same id.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return "animation-" + Convert.ToHexStringLower(hash.AsSpan(0, 4));
    }

    private CustomAnimation? LoadCore(string id)
    {
        var path = PathFor(id);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > CustomAnimationParser.MaxDocumentLength)
            {
                return null;
            }

            var result = CustomAnimationParser.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (result.Animation is null)
            {
                _logger.LogWarning("Skipping custom animation {Id}: {Error}", id, result.Error);
            }

            return result.Animation;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read custom animation {Id}.", id);
            return null;
        }
    }

    private bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not delete {Path}.", path);
            return false;
        }
    }

    private string PathFor(string id) => Path.Combine(_folder, id + FileExtension);

    private static CustomAnimationInfo ToInfo(string id, CustomAnimation animation) =>
        new(id, animation.Name, animation.Description, animation.Frames.Count, animation.TotalDuration);

    private static string DefaultFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Porchlight",
            "Animations");
}
