namespace Porchlight.Core.Network;

/// <summary>Result of asking Windows' connectivity test address for its expected answer.</summary>
public enum InternetCheckResult
{
    /// <summary>Got the expected answer: the internet is reachable.</summary>
    Reachable,

    /// <summary>Got a redirect or a different page: the network wants a sign-in first.</summary>
    CaptivePortal,

    /// <summary>No answer.</summary>
    Unreachable,
}
