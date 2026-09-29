namespace Porchlight.Core.Network;

/// <summary>Where a troubleshooter step is, or how it ended.</summary>
public enum StepState
{
    /// <summary>Not started yet.</summary>
    Pending,

    /// <summary>Running now.</summary>
    Running,

    /// <summary>Worked.</summary>
    Passed,

    /// <summary>Did not work.</summary>
    Failed,

    /// <summary>Not run because an earlier step already showed the problem.</summary>
    Skipped,

    /// <summary>The internet check was answered by a sign-in page (hotel, cafe or similar Wi-Fi).</summary>
    NeedsSignIn,
}
