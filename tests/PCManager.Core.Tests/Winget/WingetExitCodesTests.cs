using PCManager.Core.Winget;
using Xunit;

namespace PCManager.Core.Tests.Winget;

public sealed class WingetExitCodesTests
{
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
