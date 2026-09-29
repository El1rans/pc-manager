namespace Porchlight.Core.Startup;

/// <summary>Finds the executable inside a <c>Run</c> value's command line. Pure string work: the
/// command is never executed.</summary>
public static class StartupCommandParser
{
    private const string ExecutableExtension = ".exe";

    /// <summary>
    /// Returns the program path from <paramref name="command"/> (quoted path, unquoted path with
    /// spaces up to its first <c>.exe</c>, or the first token), with environment variables
    /// expanded; null for an empty command.
    /// </summary>
    public static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(command.Trim());

        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            var quoted = close > 1 ? text[1..close] : text[1..];
            return string.IsNullOrWhiteSpace(quoted) ? null : quoted.Trim();
        }

        var exe = text.IndexOf(ExecutableExtension, StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            return text[..(exe + ExecutableExtension.Length)];
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }
}
