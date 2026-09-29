namespace Porchlight.Core.Network;

/// <summary>How a remedy ended.</summary>
public enum RemedyOutcome
{
    /// <summary>Done.</summary>
    Done,

    /// <summary>Needs administrator rights, which Porchlight does not have right now.</summary>
    NeedsAdmin,

    /// <summary>Did not work.</summary>
    Failed,
}
