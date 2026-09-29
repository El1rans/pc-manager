namespace Porchlight.Core.Health;

/// <summary>What <c>DISM /Online /Cleanup-Image /RestoreHealth</c> reported.</summary>
public enum DismOutcome
{
    /// <summary>The restore completed successfully.</summary>
    Succeeded,

    /// <summary>DISM could not find the files it needs (no internet, or Windows Update blocked).</summary>
    SourceNotFound,

    /// <summary>DISM needs administrator rights (error 740).</summary>
    NeedsAdmin,

    /// <summary>Any other failure.</summary>
    Failed,
}
