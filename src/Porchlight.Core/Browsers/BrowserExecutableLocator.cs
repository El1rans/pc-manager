using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserExecutableLocator"/>
public sealed class BrowserExecutableLocator : IBrowserExecutableLocator
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

    private readonly ILogger<BrowserExecutableLocator> _logger;

    public BrowserExecutableLocator(ILogger<BrowserExecutableLocator> logger)
    {
        _logger = logger;
    }

    public string? Find(BrowserKind kind)
    {
        var exe = BrowserAddOnsOpener.ExecutableName(kind);
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(AppPathsKey + exe);
                var path = (key?.GetValue(null) as string)?.Trim().Trim('"');
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Safe to ignore: a hive we cannot read just means we try the next one, and then
                // report "browser not found" to the user.
                _logger.LogWarning(ex, "Could not read App Paths for {Exe}.", exe);
            }
        }

        return null;
    }
}
