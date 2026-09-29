namespace Porchlight.Core.Network;

/// <summary>Pure state machine: which troubleshooter step failed -> which advice and fixes.</summary>
public static class NetworkDiagnosis
{
    /// <summary>Decides the diagnosis from each step's state. A missing step counts as skipped.</summary>
    public static Diagnosis Diagnose(IReadOnlyDictionary<TroubleshootStep, StepState> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        StepState Get(TroubleshootStep step) => results.GetValueOrDefault(step, StepState.Skipped);

        // The internet answer wins: many routers ignore pings, so a failed router ping alone is
        // not a problem when the internet itself works.
        if (Get(TroubleshootStep.Internet) == StepState.Passed)
        {
            return new Diagnosis(
                DiagnosisOutcome.Healthy,
                "Your internet is working.",
                "If one website or app still doesn't work, the problem is likely with that site or app, not your connection.",
                []);
        }

        if (Get(TroubleshootStep.Internet) == StepState.NeedsSignIn)
        {
            return new Diagnosis(
                DiagnosisOutcome.SignInRequired,
                "This network wants you to sign in first.",
                "Open your web browser and try any website. A sign-in page should appear. Follow its steps, then check again.",
                []);
        }

        if (Get(TroubleshootStep.Adapter) == StepState.Failed)
        {
            return new Diagnosis(
                DiagnosisOutcome.AdapterDown,
                "Your computer's network connection is switched off or unplugged.",
                "Check that the network cable is plugged in, or that Wi-Fi is turned on. Then try switching the connection off and on again.",
                [RemedyKind.ResetAdapter, RemedyKind.OpenNetworkSettings]);
        }

        if (Get(TroubleshootStep.Router) == StepState.Failed)
        {
            return new Diagnosis(
                DiagnosisOutcome.RouterUnreachable,
                "Your computer can't reach your router.",
                "Check that your router (the box from your internet provider) is on. If you use Wi-Fi, move closer to it. Then try the fixes below.",
                [RemedyKind.RenewIp, RemedyKind.ResetAdapter]);
        }

        if (Get(TroubleshootStep.NameLookup) == StepState.Failed)
        {
            return new Diagnosis(
                DiagnosisOutcome.DnsProblem,
                "Your computer can't look up website names.",
                "This is often fixed by clearing saved website addresses. If that doesn't help, ask for a fresh connection.",
                [RemedyKind.FlushDns, RemedyKind.RenewIp]);
        }

        return new Diagnosis(
            DiagnosisOutcome.InternetUnreachable,
            "Your router is working, but the internet isn't getting through.",
            "Turn your router off, wait 30 seconds and turn it on again. If that doesn't help, the problem may be with your internet provider - call them.",
            [RemedyKind.RenewIp, RemedyKind.OpenNetworkSettings]);
    }
}
