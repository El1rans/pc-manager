namespace Porchlight.Core.Network;

/// <summary>Plain-language names and results for the troubleshooter steps.</summary>
public static class TroubleshootStepText
{
    /// <summary>Short name of a step.</summary>
    public static string Title(TroubleshootStep step) => step switch
    {
        TroubleshootStep.Adapter => "Network connection",
        TroubleshootStep.Router => "Your router",
        TroubleshootStep.NameLookup => "Website names",
        TroubleshootStep.Internet => "The internet",
        _ => step.ToString(),
    };

    /// <summary>What a step's state means, in a short sentence.</summary>
    public static string Describe(TroubleshootStep step, StepState state) => state switch
    {
        StepState.Pending => "Waiting",
        StepState.Running => "Checking...",
        StepState.Skipped => "Not checked",
        StepState.NeedsSignIn => "Needs a sign-in",
        StepState.Passed => step switch
        {
            TroubleshootStep.Adapter => "Connected",
            TroubleshootStep.Router => "Your router answers",
            TroubleshootStep.NameLookup => "Website names work",
            _ => "Reachable",
        },
        StepState.Failed => step switch
        {
            TroubleshootStep.Adapter => "Not connected",
            TroubleshootStep.Router => "No answer from your router",
            TroubleshootStep.NameLookup => "Website names don't work",
            _ => "Not reachable",
        },
        _ => state.ToString(),
    };
}
