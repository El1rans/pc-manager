namespace Porchlight.Core.Winget;

/// <summary>
/// Resolves winget to an absolute path. Porchlight runs elevated and launches with
/// <c>UseShellExecute = false</c>, where a bare "winget" makes CreateProcess look in the app
/// directory and the current directory before PATH - so a planted <c>winget.exe</c> there would
/// run elevated. This checks the standard App Execution Alias location, then PATH entries only
/// (never the current directory), and falls back to the bare name so behaviour is never worse than
/// before.
/// </summary>
public static class WingetLocator
{
    private const string BareName = "winget";
    private const string FileName = "winget.exe";

    public static string Resolve() => Resolve(
        Environment.GetEnvironmentVariable("LOCALAPPDATA"),
        Environment.GetEnvironmentVariable("PATH"),
        File.Exists);

    /// <summary>Testable core of <see cref="Resolve()"/>.</summary>
    internal static string Resolve(string? localAppData, string? pathVariable, Func<string, bool> fileExists)
    {
        if (!string.IsNullOrEmpty(localAppData) && Path.IsPathRooted(localAppData))
        {
            var alias = Path.Combine(localAppData, "Microsoft", "WindowsApps", FileName);
            if (fileExists(alias))
            {
                return alias;
            }
        }

        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (var entry in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var directory = entry.Trim().Trim('"');

                // A relative entry would resolve against the current directory - exactly what this
                // exists to avoid.
                if (directory.Length == 0 || !Path.IsPathRooted(directory))
                {
                    continue;
                }

                string candidate;
                try
                {
                    candidate = Path.Combine(directory, FileName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return BareName;
    }
}
