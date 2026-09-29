namespace Porchlight.Core.Checkup;

/// <summary>How worried the helper should be about one check-up section. Ordered: a larger value is
/// worse, so the overall status of a report is simply the maximum.</summary>
public enum CheckupSeverity
{
    /// <summary>Nothing to do.</summary>
    Ok = 0,

    /// <summary>Not urgent, but worth a look.</summary>
    NeedsAttention = 1,

    /// <summary>Something is wrong and should be fixed soon.</summary>
    Problem = 2,
}
