using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupApprovedBlobTests
{
    [Fact]
    public void IsEnabled_MissingOrEmpty_IsEnabled()
    {
        Assert.True(StartupApprovedBlob.IsEnabled(null));
        Assert.True(StartupApprovedBlob.IsEnabled([]));
    }

    [Theory]
    [InlineData(0x02, true)]
    [InlineData(0x06, true)]
    [InlineData(0x03, false)]
    [InlineData(0x07, false)]
    public void IsEnabled_DecodesFirstByte(byte first, bool expected)
    {
        var blob = new byte[StartupApprovedBlob.Length];
        blob[0] = first;

        Assert.Equal(expected, StartupApprovedBlob.IsEnabled(blob));
    }

    [Fact]
    public void CreateEnabled_IsTwelveBytesStartingWithTwo()
    {
        var blob = StartupApprovedBlob.CreateEnabled();

        Assert.Equal(12, blob.Length);
        Assert.Equal(0x02, blob[0]);
        Assert.All(blob.Skip(1), b => Assert.Equal(0, b));
        Assert.True(StartupApprovedBlob.IsEnabled(blob));
    }

    [Fact]
    public void CreateDisabled_WritesThreeAndLittleEndianFileTime()
    {
        var when = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var blob = StartupApprovedBlob.CreateDisabled(when);

        Assert.Equal(12, blob.Length);
        Assert.Equal(0x03, blob[0]);
        Assert.Equal(when.ToFileTime(), BitConverter.ToInt64(blob, 4));
        Assert.False(StartupApprovedBlob.IsEnabled(blob));
    }

    [Fact]
    public void GetDisabledAt_RoundTripsAndIsNullWhenEnabled()
    {
        var when = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        Assert.Equal(when.ToFileTime(), StartupApprovedBlob.GetDisabledAt(StartupApprovedBlob.CreateDisabled(when))!.Value.ToFileTime());
        Assert.Null(StartupApprovedBlob.GetDisabledAt(StartupApprovedBlob.CreateEnabled()));
        Assert.Null(StartupApprovedBlob.GetDisabledAt(null));
    }
}
