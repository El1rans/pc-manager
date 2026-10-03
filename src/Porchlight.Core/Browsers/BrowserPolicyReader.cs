using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserPolicyReader"/>
/// <remarks>Strictly read-only. Only the Chromium policy keys are read; Firefox policies live in a
/// <c>policies.json</c> next to the install and are out of scope (see the spec).</remarks>
public sealed class BrowserPolicyReader : IBrowserPolicyReader
{
    private const string PoliciesRoot = @"Software\Policies\";
    private const int MaxStartupUrls = 50;

    private readonly ILogger<BrowserPolicyReader> _logger;

    public BrowserPolicyReader(ILogger<BrowserPolicyReader> logger)
    {
        _logger = logger;
    }

    public BrowserPolicySettings Read(BrowserKind kind)
    {
        var subKey = kind switch
        {
            BrowserKind.Chrome => PoliciesRoot + @"Google\Chrome",
            BrowserKind.Edge => PoliciesRoot + @"Microsoft\Edge",
            BrowserKind.Brave => PoliciesRoot + @"BraveSoftware\Brave",
            _ => null,
        };
        if (subKey is null)
        {
            return BrowserPolicySettings.None;
        }

        string? home = null;
        string? search = null;
        string? newTab = null;
        var urls = new List<string>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key is null)
                {
                    continue;
                }

                home ??= Text(key, "HomepageLocation");
                search ??= Text(key, "DefaultSearchProviderSearchURL");
                newTab ??= Text(key, "NewTabPageLocation");
                using var list = key.OpenSubKey("RestoreOnStartupURLs");
                if (list is not null)
                {
                    foreach (var name in list.GetValueNames().Take(MaxStartupUrls))
                    {
                        if (Text(list, name) is { } url)
                        {
                            urls.Add(url);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Safe to ignore: an unreadable policy hive just means we report nothing forced from it.
                _logger.LogWarning(ex, "Could not read {Browser} policies.", kind);
            }
        }

        return new BrowserPolicySettings(home, search, urls, newTab);
    }

    private static string? Text(RegistryKey key, string name) =>
        key.GetValue(name) is string value && value.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
