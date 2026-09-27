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

    [Fact]
    public void Describe_Zero_IsSuccess()
    {
        var (outcome, message) = WingetExitCodes.Describe(0);

        Assert.Equal(PackageOutcome.Success, outcome);
        Assert.Equal("Updated", message);
    }

    [Fact]
    public void Describe_UpdateNotApplicable_IsSkipped()
    {
        var (outcome, message) = WingetExitCodes.Describe(unchecked((int)0x8A15002B));

        Assert.Equal(PackageOutcome.Skipped, outcome);
        Assert.Equal("No applicable update", message);
    }

    [Fact]
    public void Describe_RebootRequiredToFinish_IsSuccessWithRestartMessage()
    {
        var (outcome, message) = WingetExitCodes.Describe(unchecked((int)0x8A150109));

        Assert.Equal(PackageOutcome.Success, outcome);
        Assert.Equal("Updated - restart needed", message);
    }

    [Theory]
    [InlineData(0x8A15010C)] // APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER
    [InlineData(0x800704C7)] // HRESULT_FROM_WIN32(ERROR_CANCELLED)
    public void Describe_CancelledByUser_IsFailedWithDeclinedMessage(long hexCode)
    {
        var (outcome, message) = WingetExitCodes.Describe(unchecked((int)hexCode));

        Assert.Equal(PackageOutcome.Failed, outcome);
        Assert.Equal("Cancelled - administrator approval was declined", message);
    }

    [Fact]
    public void Describe_RawWin32ErrorCancelled_IsFailedWithDeclinedMessage()
    {
        var (outcome, message) = WingetExitCodes.Describe(1223);

        Assert.Equal(PackageOutcome.Failed, outcome);
        Assert.Equal("Cancelled - administrator approval was declined", message);
    }

    [Fact]
    public void Describe_AppInUse_IsFailed()
    {
        var (outcome, message) = WingetExitCodes.Describe(unchecked((int)0x8A150101));

        Assert.Equal(PackageOutcome.Failed, outcome);
        Assert.Equal("App is running - close it", message);
    }

    [Fact]
    public void Describe_UnknownCode_IsFailedWithHexCode()
    {
        var (outcome, message) = WingetExitCodes.Describe(unchecked((int)0x87654321));

        Assert.Equal(PackageOutcome.Failed, outcome);
        Assert.Equal("Failed (0x87654321)", message);
    }

    [Fact]
    public void MentionsRestart_LineMentionsRestart_ReturnsTrue()
    {
        string[] lines = ["Successfully installed", "Restart the computer to complete this operation."];

        Assert.True(WingetExitCodes.MentionsRestart(lines));
    }

    [Fact]
    public void MentionsRestart_CaseInsensitive_ReturnsTrue()
    {
        string[] lines = ["A RESTART is required."];

        Assert.True(WingetExitCodes.MentionsRestart(lines));
    }

    [Fact]
    public void MentionsRestart_NoMention_ReturnsFalse()
    {
        string[] lines = ["Successfully installed"];

        Assert.False(WingetExitCodes.MentionsRestart(lines));
    }

    [Fact]
    public void MentionsRestart_NoLines_ReturnsFalse()
    {
        Assert.False(WingetExitCodes.MentionsRestart([]));
    }
}
