namespace Porchlight.Core.Startup;

/// <summary>One item that starts at sign-in.</summary>
/// <param name="Id">Stable id (<c>source|itemName</c>) used to ask for a change.</param>
/// <param name="Source">Where it was found.</param>
/// <param name="ItemName">Registry value name or shortcut file name (what <c>StartupApproved</c> is keyed by).</param>
/// <param name="DisplayName">Friendly name for the UI.</param>
/// <param name="Publisher">Company name, if the program reports one.</param>
/// <param name="ExecutablePath">Program path, if it could be worked out. Never launched.</param>
/// <param name="Hint">Plain "What is this?" sentence.</param>
/// <param name="RecommendedToKeep">Windows/Microsoft or Porchlight's own.</param>
/// <param name="IsEnabled">Whether it will start at the next sign-in.</param>
public sealed record StartupEntry(
    string Id,
    StartupSource Source,
    string ItemName,
    string DisplayName,
    string? Publisher,
    string? ExecutablePath,
    string Hint,
    bool RecommendedToKeep,
    bool IsEnabled)
{
    /// <summary>Changing this entry needs administrator rights.</summary>
    public bool RequiresAdmin => Source.IsPerMachine();
}
