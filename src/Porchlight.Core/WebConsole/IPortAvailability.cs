namespace Porchlight.Core.WebConsole;

/// <summary>Checks whether the web console could listen on a TCP port right now, behind an
/// interface so <see cref="WebConsoleController"/>'s first-start port choice is unit-testable.</summary>
public interface IPortAvailability
{
    /// <summary>Whether a listener can be bound to <paramref name="port"/> on every interface (the
    /// way <see cref="WebConsoleServer"/> binds it). A snapshot: another program may still take the
    /// port a moment later, which the server then reports as a normal start failure.</summary>
    bool IsFree(int port);
}
