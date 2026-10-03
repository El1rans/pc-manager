namespace Porchlight.Core.Safety;

/// <summary>Everything the Safety page knows, in one model. A later web console change can reuse this
/// through <see cref="ISafetyStatusService"/>.</summary>
/// <param name="Security">Antivirus and firewall.</param>
/// <param name="WindowsUpdate">Windows Update history and restart flag.</param>
/// <param name="RemoteAccess">Remote-control tools found.</param>
public sealed record SafetyStatus(SecurityStatus Security, WindowsUpdateStatus WindowsUpdate, RemoteAccessStatus RemoteAccess)
{
    /// <summary>How many of the three checks want a look.</summary>
    public int AttentionCount =>
        (Security.Level == SafetyLevel.Attention ? 1 : 0) +
        (WindowsUpdate.Level == SafetyLevel.Attention ? 1 : 0) +
        (RemoteAccess.Level == SafetyLevel.Attention ? 1 : 0);

    public SafetyLevel Level =>
        AttentionCount > 0 ? SafetyLevel.Attention :
        Security.Level == SafetyLevel.Unknown && WindowsUpdate.Level == SafetyLevel.Unknown ? SafetyLevel.Unknown :
        SafetyLevel.Good;

    /// <summary>One plain headline for the top of the page.</summary>
    public string Headline => AttentionCount switch
    {
        0 when Level == SafetyLevel.Unknown => "Couldn't check this PC",
        0 => "This PC looks safe",
        1 => "1 thing to look at",
        _ => $"{AttentionCount} things to look at",
    };
}
