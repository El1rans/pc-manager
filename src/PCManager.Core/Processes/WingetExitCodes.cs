namespace PCManager.Core.Processes;

/// <summary>
/// The subset of winget's HRESULT exit codes (see
/// https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md)
/// that <c>IComponentService</c> needs to give a specific, readable outcome for instead of a
/// generic "installation failed" message.
/// </summary>
public static class WingetExitCodes
{
    /// <summary>APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED - <c>winget install</c> found the
    /// package already installed. Treated as success.</summary>
    public const int PackageAlreadyInstalled = unchecked((int)0x8A150061);

    /// <summary>APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE - "No applicable update found", the
    /// upgrade-context equivalent of <see cref="PackageAlreadyInstalled"/>. Treated as success.</summary>
    public const int UpdateNotApplicable = unchecked((int)0x8A15002B);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_CANCELLED_BY_USER - the installer's own UAC/consent
    /// prompt was declined. Not a failure to report as an error; the user said no.</summary>
    public const int InstallCancelledByUser = unchecked((int)0x8A15010C);

    /// <summary>True if <paramref name="exitCode"/> means the component ended up installed even
    /// though winget did not "install" anything new this run.</summary>
    public static bool IsAlreadyInstalled(int exitCode) =>
        exitCode is PackageAlreadyInstalled or UpdateNotApplicable;

    /// <summary>True if <paramref name="exitCode"/> means the user declined an elevation or
    /// installer consent prompt.</summary>
    public static bool IsCancelledByUser(int exitCode) => exitCode == InstallCancelledByUser;
}
