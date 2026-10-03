using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class ProductStateParserTests
{
    [Theory]
    [InlineData(266240, ProductRunState.On, true)]     // 0x041000 Defender on, current
    [InlineData(266256, ProductRunState.On, false)]    // 0x041010 on, out of date
    [InlineData(262144, ProductRunState.Off, true)]    // 0x040000 off
    [InlineData(262160, ProductRunState.Off, false)]   // 0x040010 off, out of date
    [InlineData(397312, ProductRunState.On, true)]     // 0x061000 third-party on, current
    [InlineData(393216, ProductRunState.Off, true)]    // 0x060000 third-party off
    [InlineData(0x42000, ProductRunState.Snoozed, true)]
    [InlineData(0x43000, ProductRunState.Expired, true)]
    public void Parse_KnownValues(long value, ProductRunState state, bool upToDate)
    {
        var info = ProductStateParser.Parse(value);

        Assert.Equal(state, info.State);
        Assert.Equal(upToDate, info.DefinitionsUpToDate);
    }

    [Fact]
    public void Parse_UnrecognisedNibbles_AreUnknown()
    {
        var info = ProductStateParser.Parse(0x0F0F0);

        Assert.Equal(ProductRunState.Unknown, info.State);
        Assert.Null(info.DefinitionsUpToDate);
    }

    [Fact]
    public void Parse_NegativeValue_DoesNotThrow()
    {
        var info = ProductStateParser.Parse(-1);

        Assert.Equal(ProductRunState.Unknown, info.State);
    }
}
