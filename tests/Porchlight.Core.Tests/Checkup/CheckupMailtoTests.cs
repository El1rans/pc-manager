using Porchlight.Core.Checkup;
using Xunit;

namespace Porchlight.Core.Tests.Checkup;

public sealed class CheckupMailtoTests
{
    [Theory]
    [InlineData("helper@example.com", true)]
    [InlineData("a.b+c@sub.example.org", true)]
    [InlineData("", false)]
    [InlineData("no-at-sign", false)]
    [InlineData("a b@example.com", false)]
    [InlineData("a@example.com?cc=evil@example.com", false)]
    [InlineData("a@example.com,b@example.com", false)]
    public void IsValidEmail(string email, bool expected) => Assert.Equal(expected, CheckupMailto.IsValidEmail(email));

    [Fact]
    public void Build_ShortBody_IncludesRecipientSubjectAndBody()
    {
        var uri = CheckupMailto.Build("helper@example.com", "Check-up", "Line 1\nLine 2");

        Assert.StartsWith("mailto:helper@example.com?subject=Check-up&body=", uri);
        Assert.EndsWith("Line%201%0D%0ALine%202", uri);
    }

    [Fact]
    public void Build_InvalidRecipient_IsDropped()
    {
        var uri = CheckupMailto.Build("bad address", "S", "B");

        Assert.StartsWith("mailto:?subject=S", uri);
    }

    [Fact]
    public void Build_LongBody_IsTruncatedUnderLimitWithNote()
    {
        var body = string.Join("\n", Enumerable.Range(0, 500).Select(i => $"Line number {i} of the report"));

        var uri = CheckupMailto.Build("helper@example.com", "Check-up", body);

        Assert.True(uri.Length <= CheckupMailto.MaxUriLength);
        Assert.Contains(Uri.EscapeDataString("Save report"), uri);
        Assert.Contains("Line%20number%200%20", uri);
    }
}
