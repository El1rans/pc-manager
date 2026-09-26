using PCManager.Core.Processes;
using Xunit;

namespace PCManager.Core.Tests.Processes;

public sealed class WingetExitCodesTests
{
    [Theory]
    [InlineData(0x8A150061)] // APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED
    [InlineData(0x8A15002B)] // APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE
    [InlineData(0x8A15010D)] // APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED
    public void IsAlreadyInstalled_KnownCodes_ReturnsTrue(long hexCode)
    {
        Assert.True(WingetExitCodes.IsAlreadyInstalled(unchecked((int)hexCode)));
    }

    [Fact]
    public void IsAlreadyInstalled_Success_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.IsAlreadyInstalled(0));
    }

    [Theory]
    [InlineData(0x8A15010C)] // APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER
    [InlineData(0x800704C7)] // HRESULT_FROM_WIN32(ERROR_CANCELLED) - what an installer with no
                              // ExpectedReturnCodes mapping (like PawnIO's) actually exits with.
    public void IsCancelledByUser_KnownHResultCodes_ReturnsTrue(long hexCode)
    {
        Assert.True(WingetExitCodes.IsCancelledByUser(unchecked((int)hexCode)));
    }

    [Fact]
    public void IsCancelledByUser_RawWin32ErrorCancelled_ReturnsTrue()
    {
        Assert.True(WingetExitCodes.IsCancelledByUser(1223));
    }

    [Fact]
    public void IsCancelledByUser_OtherFailure_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.IsCancelledByUser(unchecked((int)0x8A150001)));
    }

    [Fact]
    public void IsRebootRequiredToFinish_KnownCode_ReturnsTrue()
    {
        Assert.True(WingetExitCodes.IsRebootRequiredToFinish(unchecked((int)0x8A150109)));
    }

    [Fact]
    public void IsRebootRequiredToFinish_Success_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.IsRebootRequiredToFinish(0));
    }
}
