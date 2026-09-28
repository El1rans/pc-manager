using Porchlight.Core.Processes;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Processes;

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
    public void DescribeOutcome_Zero_IsUpdated()
    {
        var outcome = WingetExitCodes.DescribeOutcome(0, outputLines: null);

        Assert.Equal(WingetOutcomeKind.Updated, outcome.Kind);
        Assert.Equal("Updated", outcome.Title);
        Assert.Equal(WingetSuggestedAction.None, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_ZeroWithRestartMentionInOutput_IsUpdatedRestartNeeded()
    {
        string[] lines = ["Successfully installed", "You must restart your PC to finish."];

        var outcome = WingetExitCodes.DescribeOutcome(0, lines);

        Assert.Equal(WingetOutcomeKind.UpdatedRestartNeeded, outcome.Kind);
        Assert.Equal("Updated - restart needed", outcome.Title);
    }

    [Fact]
    public void DescribeOutcome_RebootRequiredToFinish_IsUpdatedRestartNeeded()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A150109), outputLines: null);

        Assert.Equal(WingetOutcomeKind.UpdatedRestartNeeded, outcome.Kind);
        Assert.Equal(WingetSuggestedAction.None, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_RebootInitiated_IsUpdatedRestartNeeded()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A15010B), outputLines: null);

        Assert.Equal(WingetOutcomeKind.UpdatedRestartNeeded, outcome.Kind);
    }

    [Fact]
    public void DescribeOutcome_InstallTechnologyMismatch_IsReinstallRequired()
    {
        // JanDeDobbeleer.OhMyPosh on the maintainer's PC - see docs/specs/09-friendly-update-outcomes.md.
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A15008E), outputLines: null);

        Assert.Equal(WingetOutcomeKind.ReinstallRequired, outcome.Kind);
        Assert.Equal("Needs a reinstall", outcome.Title);
        Assert.Equal(WingetSuggestedAction.Reinstall, outcome.SuggestedAction);
        Assert.Equal("0x8A15008E", outcome.ExitCodeHex);
    }

    [Theory]
    [InlineData(0x8A15002B)] // APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE - RARLab.WinRAR on the maintainer's PC.
    [InlineData(0x8A150010)] // APPINSTALLER_CLI_ERROR_NO_APPLICABLE_INSTALLER
    [InlineData(0x8A150068)] // APPINSTALLER_CLI_ERROR_PACKAGE_IS_PINNED
    public void DescribeOutcome_NoApplicableCodes_IsNoApplicableUpdateWithHide(long hexCode)
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)hexCode), outputLines: null);

        Assert.Equal(WingetOutcomeKind.NoApplicableUpdate, outcome.Kind);
        Assert.Equal("Not available for this PC", outcome.Title);
        Assert.Equal(WingetSuggestedAction.Hide, outcome.SuggestedAction);
    }

    [Theory]
    [InlineData(0x8A150101)] // APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE
    [InlineData(0x8A150103)] // APPINSTALLER_CLI_ERROR_INSTALL_FILE_IN_USE
    [InlineData(0x8A150111)] // APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE_BY_APPLICATION
    public void DescribeOutcome_AppInUseCodes_IsAppRunningWithCloseAppAndRetry(long hexCode)
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)hexCode), outputLines: null);

        Assert.Equal(WingetOutcomeKind.AppRunning, outcome.Kind);
        Assert.Equal("Close the app and try again", outcome.Title);
        Assert.Equal(WingetSuggestedAction.CloseAppAndRetry, outcome.SuggestedAction);
    }

    [Theory]
    [InlineData(0x8A15010C)] // APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER
    [InlineData(0x800704C7)] // HRESULT_FROM_WIN32(ERROR_CANCELLED)
    [InlineData(1223)] // raw ERROR_CANCELLED
    public void DescribeOutcome_CancelledCodes_IsCancelledWithRetry(long code)
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)code), outputLines: null);

        Assert.Equal(WingetOutcomeKind.Cancelled, outcome.Kind);
        Assert.Equal(WingetSuggestedAction.Retry, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_CommandRequiresAdmin_IsNeedsAdmin()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A150019), outputLines: null);

        Assert.Equal(WingetOutcomeKind.NeedsAdmin, outcome.Kind);
    }

    [Theory]
    [InlineData(0x8A15003A)] // APPINSTALLER_CLI_ERROR_BLOCKED_BY_POLICY
    [InlineData(0x8A15010F)] // APPINSTALLER_CLI_ERROR_INSTALL_BLOCKED_BY_POLICY
    [InlineData(0x8A15001B)] // APPINSTALLER_CLI_ERROR_MSSTORE_BLOCKED_BY_POLICY
    [InlineData(0x8A15001C)] // APPINSTALLER_CLI_ERROR_MSSTORE_APP_BLOCKED_BY_POLICY
    public void DescribeOutcome_PolicyCodes_IsBlocked(long hexCode)
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)hexCode), outputLines: null);

        Assert.Equal(WingetOutcomeKind.Blocked, outcome.Kind);
        Assert.Equal(WingetSuggestedAction.None, outcome.SuggestedAction);
    }

    [Theory]
    [InlineData(0x8A150008)] // APPINSTALLER_CLI_ERROR_DOWNLOAD_FAILED
    [InlineData(0x8A15002E)] // APPINSTALLER_CLI_ERROR_DOWNLOAD_SIZE_MISMATCH
    [InlineData(0x8A150107)] // APPINSTALLER_CLI_ERROR_INSTALL_NO_NETWORK
    [InlineData(0x8A15006D)] // APPINSTALLER_CLI_ERROR_SERVICE_UNAVAILABLE
    [InlineData(0x8A150086)] // APPINSTALLER_CLI_ERROR_INSTALLER_ZERO_BYTE_FILE
    [InlineData(0x8A150011)] // APPINSTALLER_CLI_ERROR_INSTALLER_HASH_MISMATCH
    public void DescribeOutcome_NetworkCodes_IsNetworkProblemWithRetry(long hexCode)
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)hexCode), outputLines: null);

        Assert.Equal(WingetOutcomeKind.NetworkProblem, outcome.Kind);
        Assert.Equal(WingetSuggestedAction.Retry, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_RebootRequiredForInstall_IsFailedWithRestartPc()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A15010A), outputLines: null);

        Assert.Equal(WingetOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(WingetSuggestedAction.RestartPc, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_UnknownCode_IsFailedGenericMessageWithCodeInTooltipOnly()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x87654321), outputLines: null);

        Assert.Equal(WingetOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("Something went wrong", outcome.Title);
        Assert.DoesNotContain("87654321", outcome.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x87654321", outcome.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WingetSuggestedAction.Retry, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_ShellExecInstallFailed_IsAppRunningWithCloseAppAndRetry()
    {
        // GOG.Galaxy on the maintainer's PC - self-updating launchers often hold their own
        // installer locked while running.
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A150006), outputLines: null);

        Assert.Equal(WingetOutcomeKind.AppRunning, outcome.Kind);
        Assert.Equal("Close the app and try again", outcome.Title);
        Assert.Equal(WingetSuggestedAction.CloseAppAndRetry, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_InstallLocationRequired_IsNoApplicableUpdateWithHide()
    {
        // Blizzard.BattleNet on the maintainer's PC.
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A15005F), outputLines: null);

        Assert.Equal(WingetOutcomeKind.NoApplicableUpdate, outcome.Kind);
        Assert.Equal("Not available for this PC", outcome.Title);
        Assert.Equal(WingetSuggestedAction.Hide, outcome.SuggestedAction);
    }

    [Fact]
    public void DescribeOutcome_OutputMentionsInstallerExitCode_AppendedToExplanationOnly()
    {
        // OBSProject.OBSStudio on the maintainer's PC.
        string[] lines = ["Files modified by the installer are currently in use.", "Installer failed with exit code: 6"];

        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A150111), lines);

        Assert.Equal(WingetOutcomeKind.AppRunning, outcome.Kind);
        Assert.Contains("exit code 6", outcome.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("exit code 6", outcome.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeOutcome_OutputWithoutInstallerExitCode_ExplanationUnchanged()
    {
        string[] lines = ["No installer exit code mentioned here."];

        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A150101), lines);

        Assert.DoesNotContain("installer itself reported", outcome.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WingetOutcome_TooltipText_CombinesExplanationAndCode()
    {
        var outcome = WingetExitCodes.DescribeOutcome(unchecked((int)0x8A15008E), outputLines: null);

        Assert.EndsWith("(winget code 0x8A15008E)", outcome.TooltipText, StringComparison.Ordinal);
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
