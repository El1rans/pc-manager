namespace Porchlight.Core.Network;

/// <summary>Outcome of a remedy plus a plain-language message for the user.</summary>
public sealed record RemedyResult(RemedyOutcome Outcome, string Message);
