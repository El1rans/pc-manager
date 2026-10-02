using Porchlight.Core.RemoteSupport;
using Xunit;

namespace Porchlight.Core.Tests.RemoteSupport;

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

    [Theory]
    [InlineData("123456789", "123456789")] // already digits only
    [InlineData("1 234 567 890", "1234567890")] // formatted: spaces stripped back to digits
    public void CopyValue_NumericId_ReturnsDigitsOnlyNoSpaces(string address, string expected) =>
        Assert.Equal(expected, AnyDeskIdFormatter.CopyValue(address));

    [Fact]
    public void CopyValue_Alias_ReturnsTrimmedAliasUnchanged() =>
        Assert.Equal("mom-laptop", AnyDeskIdFormatter.CopyValue("  mom-laptop  "));

    [Fact]
    public void CopyValue_NullOrBlank_ReturnsEmpty() =>
        Assert.Equal(string.Empty, AnyDeskIdFormatter.CopyValue(null));
}
