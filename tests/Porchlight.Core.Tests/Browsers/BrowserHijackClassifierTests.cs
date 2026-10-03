using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

public sealed class BrowserHijackClassifierTests
{
    [Theory]
    [InlineData("https://www.google.com/search?q={searchTerms}", "google.com")]
    [InlineData("https://www.google.co.uk/", "google.co.uk")]
    [InlineData("{google:baseURL}search?q={searchTerms}", "google.com")]
    [InlineData("https://www.bing.com/search?q={searchTerms}", "bing.com")]
    [InlineData("https://duckduckgo.com/?q={searchTerms}", "duckduckgo.com")]
    [InlineData("https://search.yahoo.com/search?p={searchTerms}", "search.yahoo.com")]
    [InlineData("https://www.ecosia.org/search?q={searchTerms}", "ecosia.org")]
    [InlineData("https://search.brave.com/search?q={searchTerms}", "search.brave.com")]
    [InlineData("https://www.startpage.com/do/search?q={searchTerms}", "startpage.com")]
    [InlineData("https://ntp.msn.com/edge/ntp", "ntp.msn.com")]
    [InlineData("msn.com", "msn.com")]
    public void KnownProviders_AreOk_WithThePlainHost(string address, string host)
    {
        var result = BrowserHijackClassifier.Classify(address, forcedByPolicy: false);

        Assert.Equal(HijackStatus.Ok, result.Status);
        Assert.Equal(host, result.Value);
    }

    [Theory]
    [InlineData("chrome://newtab")]
    [InlineData("chrome://newtab/")]
    [InlineData("edge://newtab")]
    [InlineData("about:newtab")]
    [InlineData("about:home")]
    [InlineData("about:blank")]
    public void TheBrowsersOwnPages_AreOk(string address)
    {
        var result = BrowserHijackClassifier.Classify(address, forcedByPolicy: false);

        Assert.Equal(HijackStatus.Ok, result.Status);
        Assert.Equal(BrowserHijackClassifier.OwnPageValue, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyMeansTheBrowserDefault_AndIsOk(string? address)
    {
        var result = BrowserHijackClassifier.Classify(address, forcedByPolicy: true);

        Assert.Equal(HijackStatus.Ok, result.Status);
        Assert.Equal(BrowserHijackClassifier.DefaultValue, result.Value);
    }

    [Theory]
    [InlineData("https://www.search-results-now.com/?q={searchTerms}", "search-results-now.com")]
    [InlineData("http://yourhomepage.example/start", "yourhomepage.example")]
    [InlineData("example.net", "example.net")]
    [InlineData("evil.com:8080/x", "evil.com")]
    // Look-alikes must not pass as the real provider.
    [InlineData("https://google.evil.com/", "google.evil.com")]
    [InlineData("https://evil-google.com/", "evil-google.com")]
    [InlineData("https://google.com.evil.ru/", "google.com.evil.ru")]
    [InlineData("https://notbing.com/", "notbing.com")]
    public void AnythingElse_IsChanged_WithThePlainHost(string address, string host)
    {
        var result = BrowserHijackClassifier.Classify(address, forcedByPolicy: false);

        Assert.Equal(HijackStatus.Changed, result.Status);
        Assert.Equal(host, result.Value);
    }

    [Fact]
    public void UnfamiliarAndForcedByPolicy_IsTheStrongerWarning()
    {
        var result = BrowserHijackClassifier.Classify("https://search.better-results.example/?q={searchTerms}", forcedByPolicy: true);

        Assert.Equal(HijackStatus.ForcedByPolicy, result.Status);
        Assert.Equal("search.better-results.example", result.Value);
    }

    [Fact]
    public void KnownButForcedByPolicy_IsStillOk()
    {
        var result = BrowserHijackClassifier.Classify("https://www.bing.com/", forcedByPolicy: true);

        Assert.Equal(HijackStatus.Ok, result.Status);
    }

    [Theory]
    [InlineData(@"file:///C:/Users/me/start.html", BrowserHijackClassifier.FileValue)]
    [InlineData("javascript:alert(1)", BrowserHijackClassifier.UnusualValue)]
    public void FilesAndOddSchemes_AreChanged(string address, string value)
    {
        var result = BrowserHijackClassifier.Classify(address, forcedByPolicy: false);

        Assert.Equal(HijackStatus.Changed, result.Status);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void Unparseable_IsChanged_AndTruncated()
    {
        var result = BrowserHijackClassifier.Classify(new string('x', 200), forcedByPolicy: false);

        Assert.Equal(HijackStatus.Changed, result.Status);
        Assert.True(result.Value.Length < 70);
    }

    [Fact]
    public void ClassifyAll_WorstWins_AndListsDistinctHosts()
    {
        var result = BrowserHijackClassifier.ClassifyAll(
            ["https://www.google.com", "https://www.google.com/", "https://a.example", "https://b.example", "https://c.example"],
            forcedByPolicy: false);

        Assert.Equal(HijackStatus.Changed, result.Status);
        Assert.Equal("google.com, a.example, b.example and 1 more", result.Value);
    }

    [Fact]
    public void ClassifyAll_NothingSet_IsOkDefault()
    {
        var result = BrowserHijackClassifier.ClassifyAll([" ", string.Empty], forcedByPolicy: true);

        Assert.Equal(HijackStatus.Ok, result.Status);
        Assert.Equal(BrowserHijackClassifier.DefaultValue, result.Value);
    }
}
