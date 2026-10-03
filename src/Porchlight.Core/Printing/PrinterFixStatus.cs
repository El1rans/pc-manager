namespace Porchlight.Core.Printing;

/// <summary>How one step of the guided fix went.</summary>
public enum PrinterFixStatus
{
    /// <summary>Nothing needed doing.</summary>
    Fine,

    /// <summary>Something was wrong and the step fixed it.</summary>
    Fixed,

    /// <summary>The step did not run (nothing to act on, or an earlier step blocked it).</summary>
    Skipped,

    Failed,

    NeedsAdmin,
}
