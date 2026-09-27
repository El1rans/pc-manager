namespace Porchlight.Core.Processes;

/// <summary>How a single package's <c>winget upgrade</c> attempt turned out; drives the Updates
/// page's Status column and its "Finished: N updated, N failed, N skipped" summary. See
/// <see cref="WingetExitCodes.Describe"/>.</summary>
public enum PackageOutcome
{
    Success,
    Skipped,
    Failed,
}

/// <summary>
/// The subset of winget's HRESULT exit codes (see
/// https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md)
/// that <c>IComponentService</c> (installing a component) and the Updates page (upgrading a
/// package) each need a specific, readable outcome for instead of a generic "failed" message.
/// Both readings of the same family of codes live on this one type rather than two - an
/// "already installed"/"no applicable update" code counts as success for
/// <c>IComponentService.InstallAsync</c> (<see cref="IsAlreadyInstalled"/>) but as
/// <see cref="PackageOutcome.Skipped"/> for an upgrade (<see cref="Describe"/>), which is exactly
/// why they are exposed as two different members instead of one shared "is this ok" bool.
/// </summary>
public static class WingetExitCodes
{
    /// <summary>APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED - <c>winget install</c> found the
    /// package already installed. Treated as success.</summary>
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);

    /// <summary>APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE - "No applicable update found", the
    /// upgrade-context equivalent of <see cref="PackageAlreadyInstalled"/>. Treated as success by
    /// <see cref="IsAlreadyInstalled"/>, but as <see cref="PackageOutcome.Skipped"/> by
    /// <see cref="Describe"/> - an upgrade that found nothing to do is not the same as an install
    /// that found the package already there.</summary>
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED - "Another version of this
    /// application is already installed." Also treated as success/already-installed.</summary>
    public const int InstallAlreadyInstalled = unchecked((int)0x8A15010D);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER - winget's own documented code for
    /// "you cancelled the installation". Only actually returned when the package's manifest maps
    /// the underlying installer exit code to it via <c>ExpectedReturnCodes</c>; namazso.PawnIO's
    /// manifest does not, so a declined PawnIO UAC prompt will not surface as this code in
    /// practice - see <see cref="Win32ErrorCancelledHResult"/> and <see cref="Win32ErrorCancelled"/>.</summary>
    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);

    /// <summary>HRESULT_FROM_WIN32(ERROR_CANCELLED) - the raw Win32 "operation canceled by the
    /// user" error (1223), wrapped as an HRESULT. What an installer with no
    /// <c>ExpectedReturnCodes</c> mapping (like PawnIO's) typically exits with when its own
    /// UAC/consent prompt is declined, since winget then just passes the installer's raw exit code
    /// through.</summary>
    public const int Win32ErrorCancelledHResult = unchecked((int)0x800704C7);

    /// <summary>ERROR_CANCELLED - the same Win32 error as
    /// <see cref="Win32ErrorCancelledHResult"/>, but returned as a plain decimal process exit code
    /// (1223) rather than wrapped as an HRESULT. Some installers exit this way instead.</summary>
    public const int Win32ErrorCancelled = 1223;

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_TO_FINISH - the install itself
    /// succeeded, but a restart is needed before it takes effect.</summary>
    public const int InstallRebootRequiredToFinish = unchecked((int)0x8A150109);

    /// <summary>The install itself succeeded, but the target application was running and needed to
    /// be closed first, so winget's install step could not complete. Upgrade-only; not part of
    /// <see cref="IsAlreadyInstalled"/>/<see cref="IsCancelledByUser"/> since
    /// <c>IComponentService</c> has no equivalent case (its components are not expected to already
    /// be running while being installed).</summary>
    public const int AppInUse = unchecked((int)0x8A150101);

    /// <summary>True if <paramref name="exitCode"/> means the component ended up installed even
    /// though winget did not "install" anything new this run.</summary>
    public static bool IsAlreadyInstalled(int exitCode) =>
        exitCode is PackageAlreadyInstalled or UpdateNotApplicable or InstallAlreadyInstalled;

    /// <summary>True if <paramref name="exitCode"/> means the user declined an elevation or
    /// installer consent prompt. Covers both winget's own documented code and the raw Win32
    /// "cancelled" error that an installer with no <c>ExpectedReturnCodes</c> mapping (like
    /// PawnIO's) actually exits with.</summary>
    public static bool IsCancelledByUser(int exitCode) =>
        exitCode is InstallCancelledByUser or Win32ErrorCancelledHResult or Win32ErrorCancelled;

    /// <summary>True if <paramref name="exitCode"/> means the install succeeded but needs a
    /// restart to finish.</summary>
    public static bool IsRebootRequiredToFinish(int exitCode) => exitCode == InstallRebootRequiredToFinish;

    /// <summary>
    /// Maps a <c>winget upgrade --id ...</c> exit code to how that package's row should be
    /// reported on the Updates page. Does not look at the command's output - see
    /// <see cref="MentionsRestart"/> for the separate "Updated - restart needed" refinement applied
    /// to a plain <see cref="PackageOutcome.Success"/> (exit code 0) result whose output mentions a
    /// restart despite winget not returning <see cref="InstallRebootRequiredToFinish"/>.
    /// </summary>
    public static (PackageOutcome Outcome, string Message) Describe(int exitCode)
    {
        if (exitCode == 0)
        {
            return (PackageOutcome.Success, "Updated");
        }

        if (exitCode == UpdateNotApplicable)
        {
            return (PackageOutcome.Skipped, "No applicable update");
        }

        if (IsRebootRequiredToFinish(exitCode))
        {
            return (PackageOutcome.Success, "Updated - restart needed");
        }

        if (IsCancelledByUser(exitCode))
        {
            return (PackageOutcome.Failed, "Cancelled - administrator approval was declined");
        }

        if (exitCode == AppInUse)
        {
            return (PackageOutcome.Failed, "App is running - close it");
        }

        return (PackageOutcome.Failed, $"Failed (0x{unchecked((uint)exitCode):X8})");
    }

    /// <summary>True if any line of a successful upgrade's output mentions that a restart is
    /// needed to finish (winget itself does not always return <see cref="InstallRebootRequiredToFinish"/>
    /// for this - some installers only say so in their own output text).</summary>
    public static bool MentionsRestart(IReadOnlyList<string> outputLines)
    {
        ArgumentNullException.ThrowIfNull(outputLines);
        foreach (var line in outputLines)
        {
            if (line.Contains("restart", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
