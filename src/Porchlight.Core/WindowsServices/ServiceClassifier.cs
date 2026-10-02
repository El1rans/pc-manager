namespace Porchlight.Core.WindowsServices;

/// <summary>Pure rules for classifying a service and describing it in plain words.</summary>
public static class ServiceClassifier
{
    private const string MicrosoftCompany = "Microsoft";
    private const int MaxDescriptionLength = 160;
    private const string Ellipsis = "...";

    /// <summary>
    /// True for a Windows/Microsoft service: the executable's company starts with "Microsoft", the
    /// executable is inside the Windows folder (covers <c>svchost.exe</c>-hosted services), or the
    /// executable is unknown (treated as Windows' own so it is never changed by mistake).
    /// </summary>
    public static bool IsMicrosoft(string? executablePath, string? company, string windowsDirectory)
    {
        if (company is not null && company.TrimStart().StartsWith(MicrosoftCompany, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return true;
        }

        var root = windowsDirectory.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return executablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for kernel, file-system and recognizer drivers, which are never listed.</summary>
    public static bool IsDriver(string? serviceType) =>
        serviceType is not null && serviceType.Contains("Driver", StringComparison.OrdinalIgnoreCase);

    public static ServiceStartType ParseStartType(string? startMode, bool delayedAutoStart) =>
        startMode?.Trim().ToLowerInvariant() switch
        {
            "auto" => delayedAutoStart ? ServiceStartType.AutomaticDelayed : ServiceStartType.Automatic,
            "boot" or "system" => ServiceStartType.Automatic,
            "disabled" => ServiceStartType.Disabled,
            _ => ServiceStartType.Manual,
        };

    public static ServiceRunState ParseState(string? state) =>
        state?.Trim().ToLowerInvariant() switch
        {
            "running" => ServiceRunState.Running,
            "stopped" => ServiceRunState.Stopped,
            "start pending" => ServiceRunState.Starting,
            "stop pending" => ServiceRunState.Stopping,
            _ => ServiceRunState.Other,
        };

    public static string StartTypeLabel(ServiceStartType type) =>
        type switch
        {
            ServiceStartType.Automatic => "Starts with Windows",
            ServiceStartType.AutomaticDelayed => "Starts with Windows (delayed)",
            ServiceStartType.Manual => "Starts when needed",
            ServiceStartType.Disabled => "Turned off",
            _ => "Starts when needed",
        };

    public static string StateLabel(ServiceRunState state) =>
        state switch
        {
            ServiceRunState.Running => "Running",
            ServiceRunState.Stopped => "Stopped",
            ServiceRunState.Starting => "Starting...",
            ServiceRunState.Stopping => "Stopping...",
            _ => "Paused",
        };

    /// <summary>The first sentence of <paramref name="description"/> (capped in length), or empty.</summary>
    public static string ShortDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var text = string.Join(' ', description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '.' && text[i + 1] == ' ')
            {
                text = text[..(i + 1)];
                break;
            }
        }

        return text.Length <= MaxDescriptionLength
            ? text
            : text[..(MaxDescriptionLength - Ellipsis.Length)].TrimEnd() + Ellipsis;
    }
}
