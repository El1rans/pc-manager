using System.ComponentModel;
using System.Diagnostics;

namespace Porchlight.Core.SelfUpdate;

/// <summary>What happened when the installer was started.</summary>
public enum InstallerLaunchResult
{
    /// <summary>The installer process started; Porchlight should now exit so Setup can replace it.</summary>
    Started,

    /// <summary>The user said no to the Windows administrator (UAC) prompt.</summary>
    Declined,

    /// <summary>Windows couldn't start the installer for another reason.</summary>
    Failed,
}

/// <summary>Starts a verified installer, behind an interface so the update flow is testable
/// without launching anything.</summary>
public interface IInstallerLauncher
{
    InstallerLaunchResult Launch(string installerPath);
}

/// <inheritdoc cref="IInstallerLauncher"/>
public sealed class InstallerLauncher : IInstallerLauncher
{
    /// <summary>Silent Inno Setup run that relaunches Porchlight afterwards: <c>/RELAUNCH=1</c> is
    /// read by <c>installer/Porchlight.iss</c> (it waits for the running Porchlight to exit, and starts
    /// the new version once installed).</summary>
    public const string SilentArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1";

    private const int ErrorCancelled = 1223; // ERROR_CANCELLED: the UAC prompt was declined.

    public InstallerLaunchResult Launch(string installerPath)
    {
        try
        {
            // ShellExecute (not CreateProcess): the installer's manifest asks for admin, which only
            // ShellExecute can satisfy by showing the UAC prompt.
            using var process = Process.Start(new ProcessStartInfo(installerPath, SilentArguments)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty,
            });
            return process is null ? InstallerLaunchResult.Failed : InstallerLaunchResult.Started;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return InstallerLaunchResult.Declined;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return InstallerLaunchResult.Failed;
        }
    }
}
