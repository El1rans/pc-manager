using Porchlight.Core.RemoteSupport;
using Xunit;

namespace Porchlight.Core.Tests.RemoteSupport;

public sealed class AnyDeskConfigParserTests
{
    [Fact]
    public void Parse_IdPresent_ReturnsId()
    {
        const string text = "ad.anynet.id=123456789\nad.anynet.alias=\n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("123456789", id);
        Assert.Null(alias);
    }

    [Fact]
    public void Parse_IdMissing_ReturnsNull()
    {
        const string text = "ad.anynet.alias=mom-laptop\nad.security.update_channel=main\n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Null(id);
        Assert.Equal("mom-laptop", alias);
    }

    [Fact]
    public void Parse_AliasOnly_ReturnsAliasAndNullId()
    {
        const string text = "ad.anynet.alias=mom-laptop\n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Null(id);
        Assert.Equal("mom-laptop", alias);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsesCorrectly()
    {
        const string text = "ad.security.update_channel=main\r\nad.anynet.id=123456789\r\nad.anynet.alias=\r\n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("123456789", id);
        Assert.Null(alias);
    }

    [Fact]
    public void Parse_ExtraSpacesAroundEquals_ParsesCorrectly()
    {
        const string text = "  ad.anynet.id  =  123456789  \n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("123456789", id);
    }

    [Fact]
    public void Parse_BothIdAndAliasPresent_ReturnsBoth()
    {
        const string text = "ad.anynet.id=123456789\nad.anynet.alias=mom-laptop\n";

        var (id, alias) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("123456789", id);
        Assert.Equal("mom-laptop", alias);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsNullForBoth()
    {
        var (id, alias) = AnyDeskConfigParser.Parse(string.Empty);

        Assert.Null(id);
        Assert.Null(alias);
    }

    [Fact]
    public void Parse_UnrelatedLinesAndBlankLines_AreIgnored()
    {
        const string text = "\n  \nad.security.update_channel=main\n\nad.anynet.id=123456789\n";

        var (id, _) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("123456789", id);
    }

    [Fact]
    public void Parse_DuplicateIdKey_LastOccurrenceWins()
    {
        const string text = "ad.anynet.id=111111111\nad.anynet.id=222222222\n";

        var (id, _) = AnyDeskConfigParser.Parse(text);

        Assert.Equal("222222222", id);
    }
}
