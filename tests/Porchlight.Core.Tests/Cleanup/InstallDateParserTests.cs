using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class InstallDateParserTests
{
    [Fact]
    public void Parse_ValidYyyyMmDd_ReturnsTheDate()
    {
        Assert.Equal(new DateOnly(2021, 3, 14), InstallDateParser.Parse("20210314"));
    }

    [Fact]
    public void Parse_IgnoresSurroundingWhitespace()
    {
        Assert.Equal(new DateOnly(2021, 3, 14), InstallDateParser.Parse(" 20210314 "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("00000000")]
    [InlineData("20211345")]
    [InlineData("2021-03-14")]
    [InlineData("14/03/2021")]
    [InlineData("abcdefgh")]
    [InlineData("202103")]
    public void Parse_AnythingElse_IsNull(string? value)
    {
        Assert.Null(InstallDateParser.Parse(value));
    }
}
