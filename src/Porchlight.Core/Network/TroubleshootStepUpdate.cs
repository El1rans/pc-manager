namespace Porchlight.Core.Network;

/// <summary>Progress report for one troubleshooter step.</summary>
/// <param name="Step">Which step.</param>
/// <param name="State">Its new state.</param>
/// <param name="Detail">Plain-language result to show next to the step.</param>
public sealed record TroubleshootStepUpdate(TroubleshootStep Step, StepState State, string Detail);
