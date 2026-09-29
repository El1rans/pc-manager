namespace Porchlight.Core.Network;

/// <summary>Final result of a troubleshooter run.</summary>
/// <param name="Steps">The final state of each step.</param>
/// <param name="Diagnosis">The conclusion.</param>
public sealed record TroubleshootReport(IReadOnlyDictionary<TroubleshootStep, StepState> Steps, Diagnosis Diagnosis);
