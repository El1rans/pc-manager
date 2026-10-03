using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserAddOnsOpener"/>
public sealed class BrowserAddOnsOpener : IBrowserAddOnsOpener
{
    private readonly IBrowserExecutableLocator _locator;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<BrowserAddOnsOpener> _logger;

    public BrowserAddOnsOpener(
        IBrowserExecutableLocator locator,
        IProcessRunner processRunner,
        ILogger<BrowserAddOnsOpener> logger)
    {
        _locator = locator;
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>The add-ons page address for <paramref name="kind"/>.</summary>
    public static string AddOnsUrl(BrowserKind kind) => kind switch
    {
        BrowserKind.Edge => "edge://extensions",
        BrowserKind.Chrome => "chrome://extensions",
        BrowserKind.Brave => "brave://extensions",
        BrowserKind.Firefox => "about:addons",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The executable file name registered under App Paths for <paramref name="kind"/>.</summary>
    public static string ExecutableName(BrowserKind kind) => kind switch
    {
        BrowserKind.Edge => "msedge.exe",
        BrowserKind.Chrome => "chrome.exe",
        BrowserKind.Brave => "brave.exe",
        BrowserKind.Firefox => "firefox.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The browser's own settings page that holds <paramref name="setting"/> (start-up and
    /// home page, or search engine).</summary>
    public static string SettingsUrl(BrowserKind kind, HijackSetting setting)
    {
        var search = setting == HijackSetting.SearchEngine;
        return kind switch
        {
            BrowserKind.Edge => search ? "edge://settings/search" : "edge://settings/startHomeNTP",
            BrowserKind.Chrome => search ? "chrome://settings/search" : "chrome://settings/onStartup",
            BrowserKind.Brave => search ? "brave://settings/search" : "brave://settings/getStarted",
            BrowserKind.Firefox => search ? "about:preferences#search" : "about:preferences#home",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public bool TryOpen(BrowserKind kind) => TryStart(kind, AddOnsUrl(kind), "add-ons");

    public bool TryOpenSettings(BrowserKind kind, HijackSetting setting) =>
        TryStart(kind, SettingsUrl(kind, setting), "settings");

    private bool TryStart(BrowserKind kind, string url, string pageName)
    {
        var exe = _locator.Find(kind);
        if (exe is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Could not find {Browser} to open its {Page} page.", kind, pageName);
            }

            return false;
        }

        try
        {
            _processRunner.StartDetached(exe, [url]);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Could not start {Browser}.", kind);
            return false;
        }
    }
}
