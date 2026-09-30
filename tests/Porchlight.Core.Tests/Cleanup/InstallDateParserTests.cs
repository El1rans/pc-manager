using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class InstallDateParserTests
{
    [Theory]
    [InlineData("20210314")]
    [InlineData(" 20210314 ")] // Surrounding whitespace is ignored.
    public void Parse_ValidYyyyMmDd_ReturnsTheDate(string value)
    {
        Assert.Equal(new DateOnly(2021, 3, 14), InstallDateParser.Parse(value));
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
