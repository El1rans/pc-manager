namespace Porchlight.Core.Startup;

/// <summary>A scheduled task that runs a program when a user signs in.</summary>
/// <param name="Path">Full task path, e.g. <c>\Vendor\Updater</c> (what <see cref="ILogonTaskSource.SetEnabled"/> takes).</param>
/// <param name="Name">The task's own name.</param>
/// <param name="ExecutablePath">Program of the first exec action, with variables expanded; never run.</param>
/// <param name="IsEnabled">The task's <c>Enabled</c> flag.</param>
/// <param name="IsMachineWide">Runs as another account, or with highest privileges.</param>
public sealed record LogonTask(string Path, string Name, string? ExecutablePath, bool IsEnabled, bool IsMachineWide);
