using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public class ReleaseParserTests
{
    private const string Full = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/El1rans/porchlight/releases/tag/v0.2.0",
          "body": "- new stuff",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "Porchlight-Setup-0.2.0.exe", "browser_download_url": "https://github.com/El1rans/porchlight/releases/download/v0.2.0/Porchlight-Setup-0.2.0.exe" },
            { "name": "Porchlight-Setup-0.2.0.exe.sha256", "browser_download_url": "https://github.com/El1rans/porchlight/releases/download/v0.2.0/Porchlight-Setup-0.2.0.exe.sha256" }
          ]
        }
        """;

    [Fact]
    public void Parse_FullRelease_ReadsEverything()
    {
        var release = ReleaseParser.Parse(Full);

        Assert.NotNull(release);
        Assert.Equal(new Version(0, 2, 0), release.Version);
        Assert.Equal("v0.2.0", release.Tag);
        Assert.Equal("https://github.com/El1rans/porchlight/releases/tag/v0.2.0", release.ReleasePageUrl.AbsoluteUri);
        Assert.Equal("- new stuff", release.Notes);
        Assert.EndsWith("Porchlight-Setup-0.2.0.exe", release.InstallerUrl!.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith("Porchlight-Setup-0.2.0.exe.sha256", release.ChecksumUrl!.AbsoluteUri, StringComparison.Ordinal);
        Assert.True(release.CanInstallAutomatically);
        Assert.Equal("Porchlight-Setup-0.2.0.exe", release.InstallerFileName);
    }

    [Fact]
    public void Parse_MissingBodyAndAssets_StillReturnsReleaseWithoutDownloads()
    {
        var release = ReleaseParser.Parse("""{ "tag_name": "v1.0.0", "html_url": "https://github.com/El1rans/porchlight/releases/tag/v1.0.0" }""");

        Assert.NotNull(release);
        Assert.Null(release.Notes);
        Assert.Null(release.InstallerUrl);
        Assert.Null(release.ChecksumUrl);
        Assert.False(release.CanInstallAutomatically);
    }

    [Fact]
    public void Parse_OnlyInstallerAsset_CannotInstallAutomatically()
    {
        var release = ReleaseParser.Parse("""
            { "tag_name": "v1.0.0", "assets": [
              { "name": "Porchlight-Setup-1.0.0.exe", "browser_download_url": "https://github.com/x/y/releases/download/v1.0.0/Porchlight-Setup-1.0.0.exe" } ] }
            """);

        Assert.NotNull(release);
        Assert.NotNull(release.InstallerUrl);
        Assert.Null(release.ChecksumUrl);
        Assert.False(release.CanInstallAutomatically);
    }

    [Fact]
    public void Parse_AssetsForAnotherVersionOrName_AreIgnored()
    {
        var release = ReleaseParser.Parse("""
            { "tag_name": "v1.0.0", "assets": [
              { "name": "Porchlight-Setup-0.9.0.exe", "browser_download_url": "https://github.com/x/y/a.exe" },
              { "name": "notes.txt", "browser_download_url": "https://github.com/x/y/notes.txt" } ] }
            """);

        Assert.NotNull(release);
        Assert.Null(release.InstallerUrl);
    }

    [Theory]
    [InlineData("""{ "tag_name": "nightly" }""")]
    [InlineData("""{ "tag_name": "v1.2" }""")]
    [InlineData("""{ "tag_name": "v1.2.3.4" }""")]
    [InlineData("""{ "tag_name": "v1.2.3-beta.1" }""")]
    [InlineData("""{ "tag_name": "" }""")]
    [InlineData("""{ "name": "no tag" }""")]
    [InlineData("""{ "tag_name": 5 }""")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("")]
    public void Parse_MalformedTagOrJson_ReturnsNull(string json) =>
        Assert.Null(ReleaseParser.Parse(json));

    [Theory]
    [InlineData("""{ "tag_name": "v2.0.0", "prerelease": true }""")]
    [InlineData("""{ "tag_name": "v2.0.0", "draft": true }""")]
    public void Parse_PrereleaseOrDraft_ReturnsNull(string json) =>
        Assert.Null(ReleaseParser.Parse(json));

    [Fact]
    public void Parse_HtmlUrlOnAnotherHost_FallsBackToTheRepositoryTagPage()
    {
        var release = ReleaseParser.Parse("""{ "tag_name": "v1.0.0", "html_url": "https://evil.example/pwn" }""");

        Assert.NotNull(release);
        Assert.Equal("https://github.com/El1rans/porchlight/releases/tag/v1.0.0", release.ReleasePageUrl.AbsoluteUri);
    }

    [Theory]
    [InlineData("v0.1.0", 0, 1, 0)]
    [InlineData("0.1.0", 0, 1, 0)]
    [InlineData("V10.20.30", 10, 20, 30)]
    public void TryParseTag_Valid(string tag, int major, int minor, int build)
    {
        Assert.True(ReleaseParser.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("1.0.0", "0.9.9", true)]
    [InlineData("0.10.0", "0.9.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.1.0", "0.1.0.0", false)]
    public void IsNewer_UsesVersionOrdering(string candidate, string current, bool expected) =>
        Assert.Equal(expected, ReleaseParser.IsNewer(Version.Parse(candidate), Version.Parse(current)));

    [Fact]
    public void IsNewer_TwoPartVersionEqualsThreePartVersion() =>
        Assert.False(ReleaseParser.IsNewer(new Version(1, 2, 0), new Version(1, 2)));

    [Theory]
    [InlineData("0.1.0", 0, 1, 0)]
    [InlineData("0.1.0+abc123", 0, 1, 0)]
    [InlineData("0.2.0-beta.1+abc", 0, 2, 0)]
    public void TryParseAppVersion_StripsSuffixes(string text, int major, int minor, int build)
    {
        Assert.True(ReleaseParser.TryParseAppVersion(text, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dev")]
    public void TryParseAppVersion_Garbage_ReturnsFalse(string? text) =>
        Assert.False(ReleaseParser.TryParseAppVersion(text, out _));
}
