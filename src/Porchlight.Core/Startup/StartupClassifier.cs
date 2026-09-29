namespace Porchlight.Core.Startup;

/// <summary>Decides "Recommended to keep" and the plain "What is this?" hint for a startup item.
/// Pure: no I/O.</summary>
public static class StartupClassifier
{
    private const string MicrosoftHint = "Part of Windows or Microsoft software. It's best to leave this on.";

    private const string PorchlightHint = "Part of Porchlight or one of the tools it sets up. It's best to leave this on.";

    private const string GenericHint = "Starts by itself when you sign in to Windows. Turning it off doesn't remove the program.";

    private static readonly string[] PorchlightMarkers = ["porchlight", "anydesk", "openrgb"];

    // Ordered: the first marker found in the item's name or path wins.
    private static readonly (string Marker, string Hint)[] KnownHints =
    [
        ("onedrive", "Keeps your OneDrive files in sync. Safe to turn off if you don't use OneDrive."),
        ("teams", "Microsoft Teams chat and meetings. Safe to turn off if you don't use it; you can still open it yourself."),
        ("spotify", "Spotify music. Safe to turn off; it will still open when you start it."),
        ("discord", "Discord chat. Safe to turn off; it will still open when you start it."),
        ("steam", "The Steam game store. Safe to turn off; it will still open when you start it."),
        ("skype", "Skype calls. Safe to turn off if you don't use it."),
        ("zoom", "Zoom video calls. Safe to turn off; it opens when you join a meeting."),
        ("dropbox", "Keeps your Dropbox files in sync. Turn off only if you don't use Dropbox."),
        ("googleupdate", "Keeps Google software up to date. It's fine to leave on."),
        ("googledrive", "Keeps your Google Drive files in sync. Turn off only if you don't use it."),
        ("adobe", "Adobe helper that checks for updates. Usually safe to turn off."),
        ("itunes", "Apple helper for iPhones and music. Usually safe to turn off."),
        ("nvidia", "Graphics card helper from NVIDIA. Leave it on if you play games or use its settings."),
        ("realtek", "Sound card helper. Leave it on if your speakers or microphone need it."),
    ];

    /// <param name="itemName">Registry value name or shortcut file name.</param>
    /// <param name="executablePath">Resolved program path, if known.</param>
    /// <param name="publisher">Company name from the program's version info, if known.</param>
    /// <param name="description">File description from the program's version info, if known.</param>
    /// <param name="windowsDirectory">The Windows folder, e.g. <c>C:\Windows</c>.</param>
    public static StartupClassification Classify(
        string itemName, string? executablePath, string? publisher, string? description, string windowsDirectory)
    {
        var haystack = $"{itemName} {executablePath}".ToLowerInvariant();

        if (PorchlightMarkers.Any(haystack.Contains))
        {
            return new StartupClassification(true, PorchlightHint);
        }

        var isMicrosoft =
            (publisher?.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ?? false) ||
            IsUnderDirectory(executablePath, windowsDirectory);

        foreach (var (marker, hint) in KnownHints)
        {
            if (haystack.Contains(marker, StringComparison.Ordinal))
            {
                // A known optional program (even Microsoft's own OneDrive/Teams) is never flagged
                // "recommended to keep": its hint already says it's safe to turn off.
                return new StartupClassification(false, hint);
            }
        }

        if (isMicrosoft)
        {
            return new StartupClassification(true, MicrosoftHint);
        }

        var hintText = string.IsNullOrWhiteSpace(description) || description.Equals(itemName, StringComparison.OrdinalIgnoreCase)
            ? GenericHint
            : $"{description.Trim().TrimEnd('.')}. {GenericHint}";
        return new StartupClassification(false, hintText);
    }

    private static bool IsUnderDirectory(string? path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var prefix = directory.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
