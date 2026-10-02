namespace Porchlight.Core.WindowsServices;

/// <summary>The only thing that talks to the Service Control Manager. Callers have already checked
/// every safety rule; this just does the change and reports what happened.</summary>
public interface IServiceManager
{
    /// <summary>Display names of services that depend on <paramref name="name"/> and are not stopped.</summary>
    IReadOnlyList<string> GetRunningDependents(string name);

    /// <summary>Starts the service and waits up to <paramref name="timeout"/> for it to run. Already running counts as changed.</summary>
    ServiceChangeResult Start(string name, TimeSpan timeout);

    /// <summary>Stops the service and waits up to <paramref name="timeout"/>. Already stopped counts as changed.</summary>
    ServiceChangeResult Stop(string name, TimeSpan timeout);

    /// <summary>Changes only the start type (and the delayed flag for automatic).</summary>
    ServiceChangeResult SetStartType(string name, ServiceStartType startType);
}
