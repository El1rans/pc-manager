using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class AccessKeyGeneratorTests
{
    [Fact]
    public void Generate_returns_32_lowercase_hex_characters()
    {
        var key = AccessKeyGenerator.Generate();

        Assert.Equal(32, key.Length);
        Assert.All(key, c => Assert.True(char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Generate_returns_a_different_key_each_time() =>
        Assert.NotEqual(AccessKeyGenerator.Generate(), AccessKeyGenerator.Generate());

    [Fact]
    public void Matches_accepts_the_exact_key() =>
        Assert.True(AccessKeyGenerator.Matches("abc123", "abc123"));

    [Theory]
    [InlineData("abc123", "abc124")]
    [InlineData("abc123", "ABC123")]
    [InlineData("abc123", "abc12")]
    [InlineData("abc123", "")]
    [InlineData("abc123", null)]
    public void Matches_rejects_anything_else(string expected, string? provided) =>
        Assert.False(AccessKeyGenerator.Matches(expected, provided));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Matches_never_accepts_when_no_key_is_set(string? provided) =>
        Assert.False(AccessKeyGenerator.Matches(string.Empty, provided));
}
