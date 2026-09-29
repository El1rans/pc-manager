namespace Porchlight.Core.Network;

/// <summary>Turns a process id into a name a user would recognise.</summary>
public interface IProcessNameResolver
{
    /// <summary>Friendly name for <paramref name="pid"/>, or null if the process is gone or unreadable.</summary>
    string? GetFriendlyName(int pid);
}
