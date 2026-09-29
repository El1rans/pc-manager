using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="IStartupFolderReader"/>
public sealed partial class StartupFolderReader : IStartupFolderReader
{
    private readonly ILogger<StartupFolderReader> _logger;

    public StartupFolderReader(ILogger<StartupFolderReader> logger)
    {
        _logger = logger;
    }

    private const string ShortcutExtension = ".lnk";
    private const string DesktopIni = "desktop.ini";
    private const string ShellProgId = "WScript.Shell";

    public IReadOnlyList<StartupFolderItem> ReadFolder(StartupSource source)
    {
        var folder = source switch
        {
            StartupSource.CurrentUserFolder => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupSource.MachineFolder => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            _ => string.Empty,
        };
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            return [];
        }

        var items = new List<StartupFolderItem>();
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals(DesktopIni, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = file.EndsWith(ShortcutExtension, StringComparison.OrdinalIgnoreCase)
                ? ResolveShortcut(file)
                : null;
            items.Add(new StartupFolderItem(fileName, target));
        }

        return items;
    }

    /// <summary>Reads a shortcut's target path through <c>WScript.Shell</c>. This only reads the
    /// .lnk file; the target is not opened or run.</summary>
    private string? ResolveShortcut(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var type = Type.GetTypeFromProgID(ShellProgId);
            if (type is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(type);
            dynamic shellDynamic = shell!;
            shortcut = shellDynamic.CreateShortcut(shortcutPath);
            dynamic shortcutDynamic = shortcut!;
            string? target = shortcutDynamic.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or MissingMethodException
                                       or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
                                       or IOException or UnauthorizedAccessException)
        {
            LogUnresolved(ex, shortcutPath);
            return null;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.ReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    // Source-generated so nothing is formatted when Debug is off (CA1873).
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not resolve the shortcut {Shortcut}; showing its file name instead.")]
    private partial void LogUnresolved(Exception ex, string shortcut);
}
