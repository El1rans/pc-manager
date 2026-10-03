using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

/// <summary>Scanner tests over fixture files in a temp folder and a fake policy reader - never the real machine.</summary>
public sealed class BrowserHijackScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "porchlight-hijack-tests-" + Guid.NewGuid().ToString("N"));
    private BrowserPolicySettings _policy = BrowserPolicySettings.None;

    private sealed class Locations(IReadOnlyList<ChromiumBrowserLocation> chromium, string? firefox) : IBrowserLocations
    {
        public IReadOnlyList<ChromiumBrowserLocation> ChromiumBrowsers => chromium;

        public string? FirefoxProfilesDirectory => firefox;
    }

    private sealed class FakePolicies(Func<BrowserPolicySettings> get) : IBrowserPolicyReader
    {
        public BrowserPolicySettings Read(BrowserKind kind) => kind == BrowserKind.Chrome ? get() : BrowserPolicySettings.None;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteBytes(string relativePath, byte[] content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private Task<BrowserHijackResult> Scan(string? firefox = null) =>
        new BrowserHijackScanner(
            new Locations([new(BrowserKind.Chrome, Path.Combine(_root, "Chrome", "User Data"))], firefox),
            new FakePolicies(() => _policy),
            NullLogger<BrowserHijackScanner>.Instance).ScanAsync(CancellationToken.None);

    private static HijackFinding Find(BrowserHijackResult result, BrowserKind browser, HijackSetting setting, string? profile = null) =>
        Assert.Single(result.Findings, f => f.Browser == browser && f.Setting == setting && f.ProfileName == profile);

    private void WriteChrome(string preferences, string? securePreferences = null)
    {
        Write(@"Chrome\User Data\Local State", """{"profile":{"info_cache":{"Default":{"name":"Grandma"}}}}""");
        Write(@"Chrome\User Data\Default\Preferences", preferences);
        if (securePreferences is not null)
        {
            Write(@"Chrome\User Data\Default\Secure Preferences", securePreferences);
        }
    }

    [Fact]
    public async Task Chrome_DefaultsAndKnownProviders_AreAllOk()
    {
        WriteChrome("""
            {"homepage":"https://www.google.com","homepage_is_newtabpage":false,
             "session":{"restore_on_startup":5},
             "default_search_provider_data":{"template_url_data":{"url":"https://www.bing.com/search?q={searchTerms}"}}}
            """);

        var result = await Scan();

        Assert.Equal([BrowserKind.Chrome], result.BrowsersChecked);
        Assert.All(result.Findings, f => Assert.Equal(HijackStatus.Ok, f.Status));
        Assert.Equal("google.com", Find(result, BrowserKind.Chrome, HijackSetting.HomePage, "Grandma").Value);
        Assert.Equal("bing.com", Find(result, BrowserKind.Chrome, HijackSetting.SearchEngine, "Grandma").Value);
        Assert.Equal(BrowserHijackClassifier.OwnPageValue, Find(result, BrowserKind.Chrome, HijackSetting.StartupPages, "Grandma").Value);
    }

    [Fact]
    public async Task Chrome_ChangedHomeStartupAndSearch_AreFlagged_WithPlainHosts()
    {
        WriteChrome("""
            {"homepage":"http://www.hijacked-home.example/start","homepage_is_newtabpage":false,
             "session":{"restore_on_startup":4,"startup_urls":["https://www.google.com","https://promo.example/x"]},
             "default_search_provider_data":{"template_url_data":{"url":"https://search.hijacked.example/?q={searchTerms}"}}}
            """);

        var result = await Scan();

        var home = Find(result, BrowserKind.Chrome, HijackSetting.HomePage, "Grandma");
        Assert.Equal(HijackStatus.Changed, home.Status);
        Assert.Equal("hijacked-home.example", home.Value);
        var startup = Find(result, BrowserKind.Chrome, HijackSetting.StartupPages, "Grandma");
        Assert.Equal(HijackStatus.Changed, startup.Status);
        Assert.Equal("google.com, promo.example", startup.Value);
        var search = Find(result, BrowserKind.Chrome, HijackSetting.SearchEngine, "Grandma");
        Assert.Equal(HijackStatus.Changed, search.Status);
        Assert.Equal("search.hijacked.example", search.Value);
    }

    [Fact]
    public async Task Chrome_HomePageIsNewTab_IgnoresAStaleHomepageValue()
    {
        WriteChrome("""{"homepage":"http://stale.example","homepage_is_newtabpage":true}""");

        var result = await Scan();

        var home = Find(result, BrowserKind.Chrome, HijackSetting.HomePage, "Grandma");
        Assert.Equal(HijackStatus.Ok, home.Status);
    }

    [Fact]
    public async Task Chrome_StartupUrlsAreIgnored_WhenNotSetToOpenSpecificPages()
    {
        WriteChrome("""{"session":{"restore_on_startup":1,"startup_urls":["http://stale.example"]}}""");

        var result = await Scan();

        Assert.Equal(HijackStatus.Ok, Find(result, BrowserKind.Chrome, HijackSetting.StartupPages, "Grandma").Status);
    }

    [Fact]
    public async Task Chrome_SecurePreferencesWinOverPreferences()
    {
        WriteChrome(
            """{"default_search_provider_data":{"template_url_data":{"url":"https://www.google.com/search?q={searchTerms}"}}}""",
            """{"default_search_provider_data":{"template_url_data":{"url":"https://secure.hijack.example/?q={searchTerms}"}}}""");

        var result = await Scan();

        Assert.Equal("secure.hijack.example", Find(result, BrowserKind.Chrome, HijackSetting.SearchEngine, "Grandma").Value);
    }

    [Fact]
    public async Task Chrome_FileHeldOpenByTheBrowser_IsStillRead()
    {
        WriteChrome("""{"homepage":"http://held-open.example"}""");
        var path = Path.Combine(_root, "Chrome", "User Data", "Default", "Preferences");
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        var result = await Scan();

        Assert.Equal("held-open.example", Find(result, BrowserKind.Chrome, HijackSetting.HomePage, "Grandma").Value);
    }

    [Fact]
    public async Task Chrome_MalformedPreferences_AreSkippedAndCounted_NotFatal()
    {
        WriteChrome("{ this is not json");

        var result = await Scan();

        Assert.Equal(1, result.SkippedCount);
        Assert.All(result.Findings, f => Assert.Equal(HijackStatus.Ok, f.Status));
    }

    [Fact]
    public async Task Chrome_PolicyForcedUnknown_IsForced_AndReplacesTheProfileFinding()
    {
        WriteChrome("""
            {"homepage":"http://profile-home.example",
             "default_search_provider_data":{"template_url_data":{"url":"https://profile-search.example/?q={searchTerms}"}}}
            """);
        _policy = new BrowserPolicySettings(
            "https://policy-home.example/", "https://policy-search.example/?q={searchTerms}", ["https://policy-start.example"], "https://policy-ntp.example");

        var result = await Scan();

        var home = Find(result, BrowserKind.Chrome, HijackSetting.HomePage);
        Assert.Equal(HijackStatus.ForcedByPolicy, home.Status);
        Assert.Equal("policy-home.example", home.Value);
        Assert.Equal(HijackStatus.ForcedByPolicy, Find(result, BrowserKind.Chrome, HijackSetting.SearchEngine).Status);
        Assert.Equal(HijackStatus.ForcedByPolicy, Find(result, BrowserKind.Chrome, HijackSetting.StartupPages).Status);
        Assert.Equal(HijackStatus.ForcedByPolicy, Find(result, BrowserKind.Chrome, HijackSetting.NewTabPage).Status);
        Assert.DoesNotContain(result.Findings, f => f.ProfileName == "Grandma" && f.Setting != HijackSetting.NewTabPage);
    }

    [Fact]
    public async Task Chrome_PolicyForcingAKnownProvider_IsOk()
    {
        WriteChrome("{}");
        _policy = new BrowserPolicySettings("https://www.google.com", null, [], null);

        var result = await Scan();

        Assert.Equal(HijackStatus.Ok, Find(result, BrowserKind.Chrome, HijackSetting.HomePage).Status);
    }

    [Fact]
    public async Task BrowserNotInstalled_IsNotReported()
    {
        var result = await Scan();

        Assert.Empty(result.BrowsersChecked);
        Assert.Empty(result.Findings);
    }

    private string FirefoxProfiles => Path.Combine(_root, "Firefox", "Profiles");

    [Fact]
    public async Task Firefox_HomepageAndNewTabFromPrefs_UserJsWins()
    {
        Write(@"Firefox\Profiles\ab12cd34.default-release\prefs.js", """
            // Mozilla User Preferences
            user_pref("browser.startup.homepage", "https://www.mozilla.org/|https://prefs-home.example/");
            user_pref("browser.startup.page", 1);
            user_pref("browser.newtab.url", "https://newtab.example/");
            user_pref("other.thing", true);
            """);
        Write(@"Firefox\Profiles\ab12cd34.default-release\user.js",
            """user_pref("browser.startup.homepage", "https://user-js-home.example/");""");

        var result = await Scan(FirefoxProfiles);

        Assert.Contains(BrowserKind.Firefox, result.BrowsersChecked);
        var home = Find(result, BrowserKind.Firefox, HijackSetting.HomePage, "default-release");
        Assert.Equal(HijackStatus.Changed, home.Status);
        Assert.Equal("user-js-home.example", home.Value);
        var tab = Find(result, BrowserKind.Firefox, HijackSetting.NewTabPage, "default-release");
        Assert.Equal("newtab.example", tab.Value);
    }

    [Fact]
    public async Task Firefox_NothingSet_IsOk_AndNewTabIsNotChecked()
    {
        Write(@"Firefox\Profiles\x.default\prefs.js", """user_pref("other.thing", 1);""");

        var result = await Scan(FirefoxProfiles);

        Assert.Equal(HijackStatus.Ok, Find(result, BrowserKind.Firefox, HijackSetting.HomePage, "default").Status);
        Assert.DoesNotContain(result.Findings, f => f.Browser == BrowserKind.Firefox && f.Setting == HijackSetting.NewTabPage);
    }

    [Fact]
    public async Task Firefox_SearchEngine_ReadFromMozLz4()
    {
        Write(@"Firefox\Profiles\x.default\prefs.js", "");
        WriteBytes(@"Firefox\Profiles\x.default\search.json.mozlz4", MozLz4DecoderTests.MozLz4("""
            {"engines":[
              {"id":"google@search.mozilla.org","_name":"Google","_isAppProvided":true,"_urls":[{"template":"https://www.google.com/search?q={searchTerms}"}]},
              {"id":"abc-123","_name":"Better Results","_isAppProvided":false,"_urls":[{"template":"https://search.better-results.example/?q={searchTerms}"}]}],
             "metaData":{"defaultEngineId":"abc-123"}}
            """));

        var result = await Scan(FirefoxProfiles);

        var search = Find(result, BrowserKind.Firefox, HijackSetting.SearchEngine, "default");
        Assert.Equal(HijackStatus.Changed, search.Status);
        Assert.Equal("search.better-results.example", search.Value);
    }

    [Fact]
    public async Task Firefox_AppProvidedEngine_IsOk_AndOlderCurrentNameIsUnderstood()
    {
        Write(@"Firefox\Profiles\x.default\prefs.js", "");
        WriteBytes(@"Firefox\Profiles\x.default\search.json.mozlz4", MozLz4DecoderTests.MozLz4("""
            {"engines":[{"_name":"Qwant","_isAppProvided":true,"_urls":[{"template":"https://www.qwant.com/?q={searchTerms}"}]}],
             "metaData":{"current":"Qwant"}}
            """));

        var result = await Scan(FirefoxProfiles);

        var search = Find(result, BrowserKind.Firefox, HijackSetting.SearchEngine, "default");
        Assert.Equal(HijackStatus.Ok, search.Status);
        Assert.Equal("Qwant", search.Value);
    }

    [Fact]
    public async Task Firefox_CorruptSearchFile_IsSkippedAndCounted_WithDefaultShown()
    {
        Write(@"Firefox\Profiles\x.default\prefs.js", "");
        WriteBytes(@"Firefox\Profiles\x.default\search.json.mozlz4", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]);

        var result = await Scan(FirefoxProfiles);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(HijackStatus.Ok, Find(result, BrowserKind.Firefox, HijackSetting.SearchEngine, "default").Status);
    }

    [Fact]
    public void ParsePrefs_HandlesEscapesNumbersAndGarbage()
    {
        var prefs = new Dictionary<string, string>();

        FirefoxSettingsReader.ParsePrefs(
            [
                """user_pref("a.string", "he said \"hi\" é");""",
                "user_pref(\"a.number\", 42);",
                "user_pref(\"a.bool\", true);",
                "not a pref line",
                "user_pref(\"broken\"",
            ],
            prefs);

        Assert.Equal("he said \"hi\" é", prefs["a.string"]);
        Assert.Equal("42", prefs["a.number"]);
        Assert.Equal("true", prefs["a.bool"]);
        Assert.Equal(3, prefs.Count);
    }
}
