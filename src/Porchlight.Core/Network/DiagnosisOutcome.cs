namespace Porchlight.Core.Network;

/// <summary>What the troubleshooter concluded.</summary>
public enum DiagnosisOutcome
{
    /// <summary>The internet works.</summary>
    Healthy,

    /// <summary>The network needs a sign-in page to be completed first.</summary>
    SignInRequired,

    /// <summary>The network adapter is off or unplugged.</summary>
    AdapterDown,

    /// <summary>The PC cannot reach the router.</summary>
    RouterUnreachable,

    /// <summary>The router answers but website names cannot be looked up.</summary>
    DnsProblem,

    /// <summary>The router answers and names resolve, but the internet is unreachable.</summary>
    InternetUnreachable,
}
