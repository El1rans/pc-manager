using PCManager.Core.RemoteSupport;
using Xunit;

namespace PCManager.Core.Tests.RemoteSupport;

public sealed class AnyDeskIdFormatterTests
{
    [Theory]
    [InlineData("123456789", "123 456 789")]
    [InlineData("1234567890", "1 234 567 890")]
    [InlineData("12", "12")]
    [InlineData("123", "123")]
    public void Format_NumericId_GroupsInThreesFromTheRight(string id, string expected) =>
        Assert.Equal(expected, AnyDeskIdFormatter.Format(id));

    [Theory]
    [InlineData("dad@example.com")]
    [InlineData("mom-laptop")]
    public void Format_Alias_LeftUnchanged(string alias) =>
        Assert.Equal(alias, AnyDeskIdFormatter.Format(alias));

    [Fact]
    public void Format_TrimsSurroundingWhitespace() =>
        Assert.Equal("123 456 789", AnyDeskIdFormatter.Format("  123456789  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_NullOrBlank_ReturnsEmpty(string? address) =>
        Assert.Equal(string.Empty, AnyDeskIdFormatter.Format(address));

    [Fact]
    public void CopyValue_NumericId_ReturnsDigitsOnlyNoSpaces() =>
        Assert.Equal("123456789", AnyDeskIdFormatter.CopyValue("123456789"));

    [Fact]
    public void CopyValue_FormattedNumericId_StripsSpacesBackToDigits() =>
        Assert.Equal("1234567890", AnyDeskIdFormatter.CopyValue("1 234 567 890"));

    [Fact]
    public void CopyValue_Alias_ReturnsTrimmedAliasUnchanged() =>
        Assert.Equal("mom-laptop", AnyDeskIdFormatter.CopyValue("  mom-laptop  "));

    [Fact]
    public void CopyValue_NullOrBlank_ReturnsEmpty() =>
        Assert.Equal(string.Empty, AnyDeskIdFormatter.CopyValue(null));
}
