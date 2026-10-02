using Porchlight.Core.Startup;

namespace Porchlight.Core.WindowsServices;

/// <summary>Finds the executable in a service's <c>PathName</c>. Pure string work.</summary>
public static class ServiceImagePathParser
{
    private const string NativePrefix = @"\??\";
    private const string SystemRootPrefix = @"\SystemRoot\";
    private const string System32Prefix = @"system32\";

    /// <summary>
    /// Returns the executable path, or null when <paramref name="pathName"/> is empty. Handles a
    /// quoted path, an unquoted path with spaces (up to <c>.exe</c>), environment variables, the
    /// <c>\??\</c> prefix, <c>\SystemRoot\</c> and a bare <c>system32\</c> relative path (both
    /// resolved against <paramref name="windowsDirectory"/>). Reuses <see cref="StartupCommandParser"/>
    /// for the quoting rules.
    /// </summary>
    public static string? ExtractExecutablePath(string? pathName, string windowsDirectory)
    {
        if (string.IsNullOrWhiteSpace(pathName))
        {
            return null;
        }

        var text = pathName.Trim();
        if (text.StartsWith(NativePrefix, StringComparison.Ordinal))
        {
            text = text[NativePrefix.Length..];
        }
        else if (text.StartsWith(SystemRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            text = Path.Combine(windowsDirectory, text[SystemRootPrefix.Length..]);
        }
        else if (text.StartsWith(System32Prefix, StringComparison.OrdinalIgnoreCase))
        {
            text = Path.Combine(windowsDirectory, text);
        }

        return StartupCommandParser.ExtractExecutablePath(text);
    }
}
