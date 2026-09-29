using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

/// <summary>Scanner tests over fixture files in a temp folder - never the real machine's profiles.</summary>
public sealed class BrowserExtensionScannerTests : IDisposable
{
    private const string IdA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string IdB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string IdComponent = "cccccccccccccccccccccccccccccccc";
    private const string IdDefaultApp = "dddddddddddddddddddddddddddddddd";
    private const string IdBadManifest = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "porchlight-browser-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class Locations(IReadOnlyList<ChromiumBrowserLocation> chromium, string? firefox) : IBrowserLocations
    {
        public IReadOnlyList<ChromiumBrowserLocation> ChromiumBrowsers => chromium;

        public string? FirefoxProfilesDirectory => firefox;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<BrowserScanResult> Scan(string? firefox = null)
    {
        var scanner = new BrowserExtensionScanner(
            new Locations([new(BrowserKind.Chrome, Path.Combine(_root, "Chrome", "User Data"))], firefox),
            _time,
            NullLogger<BrowserExtensionScanner>.Instance);
        return scanner.ScanAsync(CancellationToken.None);
    }

    private static string InstallTimeFor(DateTimeOffset when) =>
        (when.UtcDateTime.ToFileTimeUtc() / 10).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void WriteChromeProfile()
    {
        var recent = InstallTimeFor(_time.GetUtcNow().AddDays(-2));
        Write(@"Chrome\User Data\Local State",
            """{"profile":{"info_cache":{"Default":{"name":"Grandma"}}}}""");
        Write(@"Chrome\User Data\Default\Preferences", """{"extensions":{"settings":{}}}""");
        Write(@"Chrome\User Data\Default\Secure Preferences", $$$$"""
            {"extensions":{"settings":{
              "{{{{IdA}}}}":{"location":1,"from_webstore":true,"state":1,"install_time":"{{{{recent}}}}","path":"{{{{IdA}}}}\\1.0_0"},
              "{{{{IdB}}}}":{"location":2,"from_webstore":false,"state":0,"path":"{{{{IdB}}}}\\2.0_0"},
              "{{{{IdComponent}}}}":{"location":5,"path":"{{{{IdComponent}}}}\\1.0"},
              "{{{{IdDefaultApp}}}}":{"location":1,"was_installed_by_default":true,"path":"{{{{IdDefaultApp}}}}\\1.0"},
              "{{{{IdBadManifest}}}}":{"location":1,"from_webstore":true,"path":"{{{{IdBadManifest}}}}\\1.0"}
            }}}
            """);
        Write($@"Chrome\User Data\Default\Extensions\{IdA}\1.0_0\manifest.json", """
            {"name":"__MSG_appName__","description":"__MSG_appDesc__","version":"1.0","default_locale":"en",
             "permissions":["history","https://*/*"],"host_permissions":["<all_urls>"]}
            """);
        Write($@"Chrome\User Data\Default\Extensions\{IdA}\1.0_0\_locales\en\messages.json",
            """{"appName":{"message":"Cool Tool"},"APPDESC":{"message":"Does cool things"}}""");
        Write($@"Chrome\User Data\Default\Extensions\{IdB}\2.0_0\manifest.json",
            """{"name":"Sideloaded","version":"2.0","permissions":["proxy"]}""");
        Write($@"Chrome\User Data\Default\Extensions\{IdComponent}\1.0\manifest.json", """{"name":"Built in"}""");
        Write($@"Chrome\User Data\Default\Extensions\{IdBadManifest}\1.0\manifest.json", "{ not json");
    }

    [Fact]
    public async Task Chromium_ListsExtensionsWithProfileNameAndSkipsBuiltIns()
    {
        WriteChromeProfile();

        var result = await Scan();

        Assert.Equal([BrowserKind.Chrome], result.BrowsersFound);
        Assert.Equal(2, result.Extensions.Count);
        Assert.Equal(1, result.SkippedCount);

        var a = result.Extensions.Single(e => e.Extension.Id == IdA).Extension;
        Assert.Equal("Grandma", a.ProfileName);
        Assert.Equal("Cool Tool", a.Name);
        Assert.Equal("Does cool things", a.Description);
        Assert.True(a.Enabled);
        Assert.Equal(ExtensionSource.Store, a.Source);
        Assert.Contains("history", a.Permissions);
        Assert.Contains("https://*/*", a.HostPermissions);
        Assert.Contains("<all_urls>", a.HostPermissions);
        Assert.NotNull(a.InstalledUtc);
        Assert.True((_time.GetUtcNow() - a.InstalledUtc!.Value).TotalDays is > 1.9 and < 2.1);

        var b = result.Extensions.Single(e => e.Extension.Id == IdB);
        Assert.False(b.Extension.Enabled);
        Assert.Equal(ExtensionSource.Sideloaded, b.Extension.Source);
        Assert.Equal(ExtensionRiskLevel.Review, b.Risk.Level);
    }

    [Fact]
    public async Task Chromium_FileOpenByBrowser_IsStillReadable()
    {
        WriteChromeProfile();
        var prefs = Path.Combine(_root, @"Chrome\User Data\Default\Secure Preferences");
        using var held = new FileStream(prefs, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        var result = await Scan();

        Assert.Equal(2, result.Extensions.Count);
    }

    [Fact]
    public async Task Chromium_MalformedPreferences_SkipsFileWithoutFailing()
    {
        Write(@"Chrome\User Data\Default\Preferences", "{{{{");

        var result = await Scan();

        Assert.Empty(result.Extensions);
        Assert.True(result.SkippedCount >= 1);
    }

    [Fact]
    public async Task Chromium_OversizedManifest_IsSkipped()
    {
        Write(@"Chrome\User Data\Default\Preferences", "{}");
        Write(@"Chrome\User Data\Default\Secure Preferences",
            $$$$"""
            {"extensions":{"settings":{"{{{{IdA}}}}":{"location":1,"from_webstore":true,"path":"{{{{IdA}}}}\\1.0"}}} }
            """);
        var manifest = Write($@"Chrome\User Data\Default\Extensions\{IdA}\1.0\manifest.json", "{}");
        using (var stream = new FileStream(manifest, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(BrowserJson.ManifestMaxBytes + 1);
        }

        var result = await Scan();

        Assert.Empty(result.Extensions);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task Chromium_NoBrowserInstalled_ReturnsEmpty()
    {
        var result = await Scan();

        Assert.Empty(result.Extensions);
        Assert.Empty(result.BrowsersFound);
    }

    [Fact]
    public async Task Firefox_ListsUserExtensionsOnly()
    {
        var installed = new DateTimeOffset(2026, 5, 30, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Write(@"Firefox\Profiles\abcd1234.default-release\extensions.json", $$$$"""
            {"addons":[
              {"id":"ublock@example.org","type":"extension","active":true,"location":"app-profile","signedState":2,
               "version":"1.0","installDate":{{{{installed}}}},"defaultLocale":{"name":"Ad blocker","description":"Blocks ads"},
               "userPermissions":{"permissions":["history"],"origins":["<all_urls>"]}},
              {"id":"side@example.org","type":"extension","active":false,"location":"app-profile","signedState":0,
               "defaultLocale":{"name":"Sideloaded"}},
              {"id":"policy@example.org","type":"extension","active":true,"location":"app-profile","signedState":2,
               "installTelemetryInfo":{"source":"enterprise-policy"},"defaultLocale":{"name":"Forced"}},
              {"id":"builtin@example.org","type":"extension","active":true,"location":"app-builtin","defaultLocale":{"name":"Built in"}},
              {"id":"theme@example.org","type":"theme","active":true,"location":"app-profile","defaultLocale":{"name":"A theme"}}
            ]}
            """);

        var result = await Scan(Path.Combine(_root, "Firefox", "Profiles"));

        Assert.Contains(BrowserKind.Firefox, result.BrowsersFound);
        Assert.Equal(3, result.Extensions.Count);

        var ublock = result.Extensions.Single(e => e.Extension.Id == "ublock@example.org").Extension;
        Assert.Equal("default-release", ublock.ProfileName);
        Assert.Equal("Ad blocker", ublock.Name);
        Assert.Equal(ExtensionSource.Store, ublock.Source);
        Assert.Contains("<all_urls>", ublock.HostPermissions);
        Assert.NotNull(ublock.InstalledUtc);

        Assert.Equal(ExtensionSource.Sideloaded, result.Extensions.Single(e => e.Extension.Id == "side@example.org").Extension.Source);
        Assert.Equal(ExtensionSource.Policy, result.Extensions.Single(e => e.Extension.Id == "policy@example.org").Extension.Source);
    }

    [Fact]
    public async Task Firefox_MalformedExtensionsJson_IsSkipped()
    {
        Write(@"Firefox\Profiles\x.default\extensions.json", "nope");

        var result = await Scan(Path.Combine(_root, "Firefox", "Profiles"));

        Assert.Empty(result.Extensions);
        Assert.Equal(1, result.SkippedCount);
    }
}
