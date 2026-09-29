namespace Porchlight.Core.Health;

/// <summary>What <c>sfc /scannow</c> reported.</summary>
public enum SfcOutcome
{
    /// <summary>Output not recognised (different language, or the tool died).</summary>
    Unknown,

    /// <summary>No integrity violations found.</summary>
    NoProblems,

    /// <summary>Corrupt files were found and repaired.</summary>
    Repaired,

    /// <summary>Corrupt files were found but some could not be repaired - DISM is offered next.</summary>
    CouldNotRepair,

    /// <summary>A repair is pending a restart, so the scan could not run.</summary>
    RebootPending,

    /// <summary>The scan could not be performed (e.g. Windows Modules Installer busy, not admin).</summary>
    CouldNotRun,
}
