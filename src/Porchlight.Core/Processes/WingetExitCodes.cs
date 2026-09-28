using System.Globalization;
using System.Text.RegularExpressions;
using Porchlight.Core.Winget;

namespace Porchlight.Core.Processes;

/// <summary>
/// The subset of winget's HRESULT exit codes (see
/// https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md,
/// symbols verified against that document) that <c>IComponentService</c> (installing a component),
/// the Updates page (upgrading a package), and <see cref="Winget.ReinstallWorkflow"/> (uninstalling
/// then installing) each need a specific, readable outcome for instead of a generic "failed"
/// message. Both readings live on this one type rather than separate tables - an "already
/// installed"/"no applicable update" code counts as success for
/// <c>IComponentService.InstallAsync</c> (<see cref="IsAlreadyInstalled"/>) but as
/// <see cref="WingetOutcomeKind.NoApplicableUpdate"/> for an upgrade
/// (<see cref="DescribeOutcome"/>) - which is exactly why they are exposed as different members
/// instead of one shared "is this ok" bool.
/// </summary>
public static partial class WingetExitCodes
{
    // --- General errors --------------------------------------------------------------------

    /// <summary>APPINSTALLER_CLI_ERROR_SHELLEXEC_INSTALL_FAILED - Windows could not even launch the
    /// installer (the <c>ShellExecute</c> call itself failed). Observed on the maintainer's PC for
    /// GOG.Galaxy ("Installer failed with exit code: 1002") - GOG Galaxy, like many game launchers,
    /// updates itself and can hold its own installer file locked while running.</summary>
    public const int ShellExecInstallFailed = unchecked((int)0x8A150006);

    /// <summary>APPINSTALLER_CLI_ERROR_NO_APPLICABLE_INSTALLER - none of the installers in the
    /// package's manifest are applicable to this system (architecture, OS version, etc.).</summary>
    public const int NoApplicableInstaller = unchecked((int)0x8A150010);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALLER_HASH_MISMATCH - the downloaded installer's hash did
    /// not match the manifest, so winget refused to run it.</summary>
    public const int InstallerHashMismatch = unchecked((int)0x8A150011);

    /// <summary>APPINSTALLER_CLI_ERROR_COMMAND_REQUIRES_ADMIN - the command needs administrator
    /// privileges to run. Porchlight never runs elevated (see <c>docs/specs/00-engineering-standards.md</c>),
    /// so this can surface for a package that requires machine-scope install.</summary>
    public const int CommandRequiresAdmin = unchecked((int)0x8A150019);

    /// <summary>APPINSTALLER_CLI_ERROR_MSSTORE_BLOCKED_BY_POLICY - the Microsoft Store client itself
    /// is blocked by an organization policy.</summary>
    public const int MsStoreBlockedByPolicy = unchecked((int)0x8A15001B);

    /// <summary>APPINSTALLER_CLI_ERROR_MSSTORE_APP_BLOCKED_BY_POLICY - this Microsoft Store app is
    /// blocked by an organization policy.</summary>
    public const int MsStoreAppBlockedByPolicy = unchecked((int)0x8A15001C);

    /// <summary>APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE - "No applicable update found", the
    /// upgrade-context equivalent of <see cref="PackageAlreadyInstalled"/>. Treated as success by
    /// <see cref="IsAlreadyInstalled"/>, but as <see cref="WingetOutcomeKind.NoApplicableUpdate"/> -
    /// <see cref="WingetOutcomeKind.NoApplicableUpdate"/> for an upgrade - an upgrade that found
    /// nothing to do is not the same as an install that found the package already there.</summary>
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);

    /// <summary>APPINSTALLER_CLI_ERROR_DOWNLOAD_FAILED - downloading the installer failed.</summary>
    public const int DownloadFailed = unchecked((int)0x8A150008);

    /// <summary>APPINSTALLER_CLI_ERROR_DOWNLOAD_SIZE_MISMATCH - the download did not match the
    /// expected content length.</summary>
    public const int DownloadSizeMismatch = unchecked((int)0x8A15002E);

    /// <summary>APPINSTALLER_CLI_ERROR_BLOCKED_BY_POLICY - the operation is blocked by an
    /// organization Group Policy.</summary>
    public const int BlockedByPolicy = unchecked((int)0x8A15003A);

    /// <summary>APPINSTALLER_CLI_ERROR_SERVICE_UNAVAILABLE - a required service is busy or
    /// unavailable; winget itself suggests trying again later.</summary>
    public const int ServiceUnavailable = unchecked((int)0x8A15006D);

    /// <summary>APPINSTALLER_CLI_ERROR_PACKAGE_IS_PINNED - the package has a pin that prevents
    /// upgrade (the Updates page's own "explicit targeting" packages can surface this).</summary>
    public const int PackageIsPinned = unchecked((int)0x8A150068);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALLER_ZERO_BYTE_FILE - the downloaded installer was zero
    /// bytes; winget attributes this to the network connection.</summary>
    public const int InstallerZeroByteFile = unchecked((int)0x8A150086);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_LOCATION_REQUIRED - this installer needs an install
    /// location that winget/Porchlight does not provide. Observed on the maintainer's PC for
    /// Blizzard.BattleNet (after several minutes - its installer likely tried, and failed, to prompt
    /// for one).</summary>
    public const int InstallLocationRequired = unchecked((int)0x8A15005F);

    /// <summary>APPINSTALLER_CLI_ERROR_UPDATE_INSTALL_TECHNOLOGY_MISMATCH - an upgrade is available
    /// but uses a different install technology than the currently installed version, so a plain
    /// <c>winget upgrade</c> cannot apply it. Observed on the maintainer's PC for
    /// <c>JanDeDobbeleer.OhMyPosh</c> - see <c>docs/specs/09-friendly-update-outcomes.md</c>. Needs
    /// an uninstall-then-install instead; see <see cref="Winget.ReinstallWorkflow"/>.</summary>
    public const int UpdateInstallTechnologyMismatch = unchecked((int)0x8A15008E);

    // --- Install errors ----------------------------------------------------------------------

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE - the application is currently
    /// running and needs to be closed first. The install step could not complete.</summary>
    public const int AppInUse = unchecked((int)0x8A150101);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_FILE_IN_USE - one or more of the application's files
    /// is currently in use; same remedy as <see cref="AppInUse"/> (close the app and try again).</summary>
    public const int InstallFileInUse = unchecked((int)0x8A150103);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_NO_NETWORK - the installer requires internet
    /// connectivity that was not available.</summary>
    public const int InstallNoNetwork = unchecked((int)0x8A150107);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_TO_FINISH - the install itself
    /// succeeded, but a restart is needed before it takes effect.</summary>
    public const int InstallRebootRequiredToFinish = unchecked((int)0x8A150109);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_REQUIRED_FOR_INSTALL - the install failed and
    /// a restart is needed before trying again (a pending restart from something else is in the
    /// way).</summary>
    public const int InstallRebootRequiredForInstall = unchecked((int)0x8A15010A);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_REBOOT_INITIATED - the installer itself is about to
    /// restart the PC to finish installation; treated like a successful "restart needed" update.</summary>
    public const int InstallRebootInitiated = unchecked((int)0x8A15010B);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER - winget's own documented code for
    /// "you cancelled the installation". Only actually returned when the package's manifest maps
    /// the underlying installer exit code to it via <c>ExpectedReturnCodes</c>; namazso.PawnIO's
    /// manifest does not, so a declined PawnIO UAC prompt will not surface as this code in
    /// practice - see <see cref="Win32ErrorCancelledHResult"/> and <see cref="Win32ErrorCancelled"/>.</summary>
    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_ALREADY_INSTALLED - "Another version of this
    /// application is already installed." Also treated as success/already-installed.</summary>
    public const int InstallAlreadyInstalled = unchecked((int)0x8A15010D);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_BLOCKED_BY_POLICY - an organization policy is
    /// preventing this specific install.</summary>
    public const int InstallBlockedByPolicy = unchecked((int)0x8A15010F);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE_BY_APPLICATION - the application is
    /// currently in use by another application; same remedy as <see cref="AppInUse"/>.</summary>
    public const int AppInUseByAnotherApplication = unchecked((int)0x8A150111);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_CUSTOM_ERROR - installation failed with an
    /// installer-specific custom error winget has no further detail for.</summary>
    public const int InstallCustomError = unchecked((int)0x8A150115);

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

    /// <summary>APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED - <c>winget install</c> found the
    /// package already installed. Treated as success.</summary>
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);

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
    /// Maps a winget exit code (from an upgrade, uninstall, or install) to a plain-language
    /// <see cref="WingetOutcome"/> for the Updates page. The command's own output text is only ever
    /// a secondary signal (see <see cref="MentionsRestart"/>) - the exit code always wins; an
    /// unrecognized code becomes <see cref="WingetOutcomeKind.Failed"/> with a generic message and
    /// the code itself only in <see cref="WingetOutcome.TooltipText"/>, never as the only thing
    /// shown. See <c>docs/specs/09-friendly-update-outcomes.md</c>.
    /// </summary>
    /// <param name="exitCode">The process exit code winget returned.</param>
    /// <param name="outputLines">The command's stdout lines, if available - used only to upgrade a
    /// plain success (exit code 0) to <see cref="WingetOutcomeKind.UpdatedRestartNeeded"/> when the
    /// output itself mentions a restart despite winget not returning
    /// <see cref="InstallRebootRequiredToFinish"/>.</param>
    public static WingetOutcome DescribeOutcome(int exitCode, IReadOnlyList<string>? outputLines)
    {
        // "Installer failed with exit code: N" - the installer's own exit code, as winget prints it
        // when it ran the installer but the installer itself then failed. Folded into every
        // installer-failure branch below's explanation (not the Title, which stays short plain
        // language) rather than a second lookup the caller has to remember to do.
        var installerExitCode = ExtractInstallerExitCode(outputLines);
        WingetOutcome Build(WingetOutcomeKind kind, string title, string explanation, WingetSuggestedAction action) =>
            new(
                kind, title,
                installerExitCode is { } code
                    ? $"{explanation} (The installer itself reported exit code {code.ToString(CultureInfo.InvariantCulture)}.)"
                    : explanation,
                exitCode, action);

        if (exitCode == 0)
        {
            return outputLines is not null && MentionsRestart(outputLines)
                ? Build(
                    WingetOutcomeKind.UpdatedRestartNeeded, "Updated - restart needed",
                    "The app was updated. Restart your PC to finish.", WingetSuggestedAction.None)
                : Build(WingetOutcomeKind.Updated, "Updated", "The app was updated.", WingetSuggestedAction.None);
        }

        if (exitCode is InstallRebootRequiredToFinish or InstallRebootInitiated)
        {
            return Build(
                WingetOutcomeKind.UpdatedRestartNeeded, "Updated - restart needed",
                "The app was updated. Restart your PC to finish.", WingetSuggestedAction.None);
        }

        if (exitCode == UpdateInstallTechnologyMismatch)
        {
            return Build(
                WingetOutcomeKind.ReinstallRequired, "Needs a reinstall",
                "The new version uses a different install method, so it can't be installed over the old one. " +
                "Reinstalling (removing the old version, then installing the new one) will fix this.",
                WingetSuggestedAction.Reinstall);
        }

        if (exitCode == UpdateNotApplicable)
        {
            // Unlike a plain "no applicable installer" (below), winget is explicit here that a
            // newer version does exist but doesn't apply to this install as-is - RARLab.WinRAR on
            // the maintainer's PC (6.24 installed, 7.23 refused). Reinstalling (uninstall, then
            // install the newest version fresh) can succeed where a plain upgrade can't, so this
            // offers both Reinstall and Hide rather than Hide alone - see
            // docs/specs/09-friendly-update-outcomes.md's addendum.
            return Build(
                WingetOutcomeKind.NoApplicableUpdate, "Not available for this PC",
                "No update that fits this PC is available right now. This isn't something you need to fix. " +
                "Reinstalling may still work, or you can hide this update.",
                WingetSuggestedAction.Reinstall | WingetSuggestedAction.Hide);
        }

        if (exitCode == NoApplicableInstaller)
        {
            return Build(
                WingetOutcomeKind.NoApplicableUpdate, "Not available for this PC",
                "No update that fits this PC is available right now. This isn't something you need to fix.",
                WingetSuggestedAction.Hide);
        }

        if (exitCode == PackageIsPinned)
        {
            return Build(
                WingetOutcomeKind.NoApplicableUpdate, "Not available for this PC",
                "This app is pinned, so it won't be updated automatically.", WingetSuggestedAction.Hide);
        }

        if (exitCode == InstallLocationRequired)
        {
            return Build(
                WingetOutcomeKind.NoApplicableUpdate, "Not available for this PC",
                "This app's installer needs to know where to install it, which Porchlight doesn't provide. " +
                "Try updating it from within the app itself, or from the Microsoft Store.",
                WingetSuggestedAction.Hide);
        }

        if (exitCode is AppInUse or InstallFileInUse or AppInUseByAnotherApplication)
        {
            return Build(
                WingetOutcomeKind.AppRunning, "Close the app and try again",
                "The app (or one of its files) is currently open, so it can't be updated. Close it, then try again.",
                WingetSuggestedAction.CloseAppAndRetry);
        }

        if (exitCode == ShellExecInstallFailed)
        {
            return Build(
                WingetOutcomeKind.AppRunning, "Close the app and try again",
                "The installer couldn't start. Many apps (especially game launchers) update themselves - " +
                "make sure the app is fully closed, including its icon in the system tray, then try again.",
                WingetSuggestedAction.CloseAppAndRetry);
        }

        if (IsCancelledByUser(exitCode))
        {
            return Build(
                WingetOutcomeKind.Cancelled, "Cancelled",
                "The update was cancelled before it finished, most likely because an approval prompt was declined.",
                WingetSuggestedAction.Retry);
        }

        if (exitCode == CommandRequiresAdmin)
        {
            return Build(
                WingetOutcomeKind.NeedsAdmin, "Needs administrator approval",
                "This update needs administrator approval that wasn't available.", WingetSuggestedAction.None);
        }

        if (exitCode is BlockedByPolicy or InstallBlockedByPolicy or MsStoreBlockedByPolicy or MsStoreAppBlockedByPolicy)
        {
            return Build(
                WingetOutcomeKind.Blocked, "Blocked by policy",
                "An organization policy on this PC is preventing this update. Contact your administrator.",
                WingetSuggestedAction.None);
        }

        if (exitCode is DownloadFailed or DownloadSizeMismatch or InstallNoNetwork or ServiceUnavailable or InstallerZeroByteFile)
        {
            return Build(
                WingetOutcomeKind.NetworkProblem, "Couldn't download the update",
                "The update couldn't be downloaded. Check your internet connection, then try again.",
                WingetSuggestedAction.Retry);
        }

        if (exitCode == InstallerHashMismatch)
        {
            return Build(
                WingetOutcomeKind.NetworkProblem, "Couldn't download the update",
                "The downloaded file didn't match what was expected, so it was rejected for safety. Try again.",
                WingetSuggestedAction.Retry);
        }

        if (exitCode == InstallRebootRequiredForInstall)
        {
            return Build(
                WingetOutcomeKind.Failed, "Restart your PC and try again",
                "Something already pending on this PC needs a restart before this update can install.",
                WingetSuggestedAction.RestartPc);
        }

        return Build(
            WingetOutcomeKind.Failed, "Something went wrong",
            "Something went wrong while updating. Details are in the log.", WingetSuggestedAction.Retry);
    }

    /// <summary>Pulls the installer's own exit code out of winget's "Installer failed with exit
    /// code: N" output line, if present - see <see cref="DescribeOutcome"/>.</summary>
    private static int? ExtractInstallerExitCode(IReadOnlyList<string>? outputLines)
    {
        if (outputLines is null)
        {
            return null;
        }

        foreach (var line in outputLines)
        {
            var match = InstallerExitCodeRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                return code;
            }
        }

        return null;
    }

    [GeneratedRegex(@"Installer failed with exit code:\s*(-?\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex InstallerExitCodeRegex();

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

    /// <summary>
    /// True if a single line of winget's <em>live</em> output says the installer is about to raise
    /// a UAC admin prompt - e.g. "The installer will request to run as administrator. Expect a
    /// prompt." Matched robustly on the "request to run as administrator" substring rather than
    /// the whole sentence, since winget's exact wording around it is not guaranteed.
    /// </summary>
    /// <remarks>
    /// Used to explain an otherwise-silent wait: winget runs as a background process, so a UAC
    /// prompt it (or the installer it launched) raises often appears only as a flashing taskbar
    /// icon, never brought to the foreground - observed on the maintainer's PC for
    /// Google.CloudSDK, which sat with no further output for several minutes waiting on exactly
    /// this. See <c>UpdatesViewModel</c>'s per-line log handling and
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum. If Porchlight itself is already
    /// running elevated, this line is never printed (there is nothing left to elevate), so no
    /// special-casing for that is needed here.
    /// </remarks>
    public static bool MentionsAdminPromptRequest(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Contains("request to run as administrator", StringComparison.OrdinalIgnoreCase);
    }
}
