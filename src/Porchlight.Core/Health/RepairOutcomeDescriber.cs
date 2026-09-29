namespace Porchlight.Core.Health;

/// <summary>Plain-language summaries of SFC / DISM outcomes.</summary>
public static class RepairOutcomeDescriber
{
    /// <summary>True when the page should now offer the DISM repair.</summary>
    public static bool OffersDism(SfcOutcome outcome) => outcome == SfcOutcome.CouldNotRepair;

    public static string Describe(SfcOutcome outcome) => outcome switch
    {
        SfcOutcome.NoProblems => "Windows files are fine. Nothing needed repairing.",
        SfcOutcome.Repaired => "Windows found damaged files and repaired them. Restart your PC to finish.",
        SfcOutcome.CouldNotRepair =>
            "Windows found damaged files but couldn't repair all of them. A deeper repair can often fix this.",
        SfcOutcome.RebootPending => "Windows is waiting for a restart before it can check. Restart your PC and try again.",
        SfcOutcome.CouldNotRun =>
            "Windows couldn't run the check right now. Restart your PC and try again; if it keeps happening, ask for help.",
        _ => "The check finished, but we couldn't tell how it went. Open the details below or ask for help.",
    };

    public static string Describe(DismOutcome outcome) => outcome switch
    {
        DismOutcome.Succeeded =>
            "The deeper repair finished. Run the file check once more to make sure everything is fixed.",
        DismOutcome.SourceNotFound =>
            "The deeper repair couldn't find the files it needs. Check the internet connection and try again.",
        DismOutcome.NeedsAdmin => "The repair needs administrator rights. Restart Porchlight as administrator.",
        _ => "The deeper repair didn't work. Restart your PC and try once more; if it fails again, ask for help.",
    };
}
