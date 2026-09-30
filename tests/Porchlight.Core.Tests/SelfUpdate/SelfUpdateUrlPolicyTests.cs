using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public class SelfUpdateUrlPolicyTests
{
    [Theory]
    [InlineData("https://github.com/El1rans/porchlight/releases/download/v0.2.0/Porchlight-Setup-0.2.0.exe")]
    [InlineData("https://GitHub.com/El1rans/porchlight/releases/download/v0.2.0/x.exe")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1/2?x=y")]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1/2?x=y")]
    public void IsAllowedDownloadUrl_GitHubHosts_Allowed(string url) =>
        Assert.True(SelfUpdateUrlPolicy.IsAllowedDownloadUrl(new Uri(url)));

    [Theory]
    [InlineData("http://github.com/El1rans/porchlight/x.exe")]
    [InlineData("https://evil.example/x.exe")]
    [InlineData("https://github.com.evil.example/x.exe")]
    [InlineData("https://evilgithub.com/x.exe")]
    [InlineData("https://raw.githubusercontent.com/El1rans/porchlight/main/x.exe")]
    [InlineData("https://api.github.com/repos/El1rans/porchlight")]
    [InlineData("https://github.com:8443/x.exe")]
    [InlineData("https://user:pass@github.com/x.exe")]
    [InlineData("ftp://github.com/x.exe")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    public void IsAllowedDownloadUrl_Others_Rejected(string url) =>
        Assert.False(SelfUpdateUrlPolicy.IsAllowedDownloadUrl(new Uri(url)));

    [Fact]
    public void IsAllowedDownloadUrl_NullOrRelative_Rejected()
    {
        Assert.False(SelfUpdateUrlPolicy.IsAllowedDownloadUrl(null));
        Assert.False(SelfUpdateUrlPolicy.IsAllowedDownloadUrl(new Uri("/x.exe", UriKind.Relative)));
    }

    [Theory]
    [InlineData("https://github.com/El1rans/porchlight/releases/tag/v1.0.0", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://objects.githubusercontent.com/x", false)]
    public void IsGitHubHttps(string url, bool expected) =>
        Assert.Equal(expected, SelfUpdateUrlPolicy.IsGitHubHttps(new Uri(url)));
}
