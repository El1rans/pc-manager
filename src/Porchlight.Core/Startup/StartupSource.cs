namespace Porchlight.Core.Startup;

/// <summary>Where a startup item was found. Each source maps to one <c>Run</c> key or Startup
/// folder and to one <c>StartupApproved</c> subkey (see <see cref="StartupSourceExtensions"/>).</summary>
public enum StartupSource
{
    /// <summary><c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
    CurrentUserRun,

    /// <summary><c>HKLM\Software\Microsoft\Windows\CurrentVersion\Run</c> (64-bit view).</summary>
    MachineRun,

    /// <summary><c>HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
    MachineRun32,

    /// <summary>The signed-in user's Startup folder.</summary>
    CurrentUserFolder,

    /// <summary>The all-users Startup folder.</summary>
    MachineFolder,

    /// <summary>A scheduled task with a logon trigger. Switched on/off through the task's own
    /// <c>Enabled</c> flag, not <c>StartupApproved</c>. Whether it needs administrator rights is
    /// per task (<see cref="StartupEntry.IsMachineWide"/>).</summary>
    LogonTask,
}
