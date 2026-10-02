namespace Porchlight.Core.Startup;

/// <summary>Facts about a <see cref="StartupSource"/>.</summary>
public static class StartupSourceExtensions
{
    /// <summary>True for a source under <c>HKLM</c> or the all-users folder - changing it needs
    /// administrator rights.</summary>
    public static bool IsPerMachine(this StartupSource source) =>
        source is StartupSource.MachineRun or StartupSource.MachineRun32 or StartupSource.MachineFolder;

    /// <summary>True for a Startup folder (as opposed to a <c>Run</c> registry key).</summary>
    public static bool IsFolder(this StartupSource source) =>
        source is StartupSource.CurrentUserFolder or StartupSource.MachineFolder;

    /// <summary>Plain-language description of who the item applies to.</summary>
    public static string ToLabel(this StartupSource source) =>
        source == StartupSource.LogonTask ? "Scheduled task" : source.IsPerMachine() ? "All users" : "Your account";
}
