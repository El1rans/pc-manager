namespace Porchlight.Core.WindowsServices;

/// <summary>How a service is started: the choices Porchlight lets the user pick plus the delayed
/// variant of automatic.</summary>
public enum ServiceStartType
{
    /// <summary>Starts with Windows.</summary>
    Automatic,

    /// <summary>Starts with Windows, a little after sign-in (delayed auto-start).</summary>
    AutomaticDelayed,

    /// <summary>Starts only when something asks for it.</summary>
    Manual,

    /// <summary>Cannot be started until it is turned back on.</summary>
    Disabled,
}
