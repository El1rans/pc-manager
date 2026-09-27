namespace Porchlight.Core.Elevation;

/// <summary>Reports and requests Windows administrator elevation for this process.</summary>
public interface IElevationService
{
    /// <summary>Whether the current process is running elevated.</summary>
    bool IsElevated { get; }

    /// <summary>
    /// Relaunches the current executable elevated (UAC prompt) and returns <see langword="true"/>
    /// if the new process was started, in which case the caller should shut this instance down.
    /// Returns <see langword="false"/> if the user cancelled the UAC prompt; that is not an error.
    /// </summary>
    bool RestartElevated();
}
