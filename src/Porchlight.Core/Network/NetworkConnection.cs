namespace Porchlight.Core.Network;

/// <summary>One established TCP connection to another computer, and the process that owns it.</summary>
/// <param name="Pid">Owning process id.</param>
public sealed record NetworkConnection(int Pid);
