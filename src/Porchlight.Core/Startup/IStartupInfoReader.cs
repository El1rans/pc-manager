namespace Porchlight.Core.Startup;

/// <summary>Reads the startup trace Windows keeps under <c>System32\wdi\LogFiles\StartupInfo</c>.</summary>
public interface IStartupInfoReader
{
    /// <summary>Reads the newest trace of the current user. Never throws: problems are logged and
    /// give an empty result (or <see cref="StartupInfoReadResult.AccessDenied"/>).</summary>
    StartupInfoReadResult Read();
}
