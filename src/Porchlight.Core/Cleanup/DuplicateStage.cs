namespace Porchlight.Core.Cleanup;

/// <summary>The step the duplicate finder is on.</summary>
public enum DuplicateStage
{
    /// <summary>Listing files and comparing sizes (no file contents are read).</summary>
    Listing,

    /// <summary>Comparing the start and end of files that have the same size.</summary>
    QuickCheck,

    /// <summary>Reading whole files to confirm they are exactly the same.</summary>
    Confirming,
}
