using PCManager.Core.Processes;
using Xunit;

namespace PCManager.Core.Tests.Processes;

public sealed class WingetExitCodesTests
{
    [Theory]
    [InlineData(0x8A150061)]
    [InlineData(0x8A15002B)]
    public void IsAlreadyInstalled_KnownCodes_ReturnsTrue(long hexCode)
    {
        Assert.True(WingetExitCodes.IsAlreadyInstalled(unchecked((int)hexCode)));
    }

    [Fact]
    public void IsAlreadyInstalled_Success_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.IsAlreadyInstalled(0));
    }

    [Fact]
    public void IsCancelledByUser_InstallCancelledCode_ReturnsTrue()
    {
        Assert.True(WingetExitCodes.IsCancelledByUser(unchecked((int)0x8A15010C)));
    }

    [Fact]
    public void IsCancelledByUser_OtherFailure_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.IsCancelledByUser(unchecked((int)0x8A150001)));
    }
}
