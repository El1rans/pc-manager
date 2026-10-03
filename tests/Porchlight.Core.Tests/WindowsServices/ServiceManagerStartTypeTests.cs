using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.Core.Tests.WindowsServices;

public sealed class ServiceManagerStartTypeTests
{
    private const uint Auto = 0x2;
    private const uint Demand = 0x3;
    private const uint Disabled = 0x4;

    [Theory]
    [InlineData(ServiceStartType.Automatic, Auto, false)]
    [InlineData(ServiceStartType.AutomaticDelayed, Auto, true)]
    [InlineData(ServiceStartType.Manual, Demand, false)]
    [InlineData(ServiceStartType.Disabled, Disabled, false)]
    public void ToNative_MapsEveryStartTypeAndOnlyDelayedSetsTheFlag(ServiceStartType type, uint start, bool delayed)
    {
        var (startValue, isDelayed) = ServiceManager.ToNative(type);

        Assert.Equal(start, startValue);
        Assert.Equal(delayed, isDelayed);
    }

    [Fact]
    public void ToNative_UnknownTypeThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ServiceManager.ToNative((ServiceStartType)99));
}
