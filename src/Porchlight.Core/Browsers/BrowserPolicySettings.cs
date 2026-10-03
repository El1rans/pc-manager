namespace Porchlight.Core.Browsers;

/// <summary>Start and search settings forced by a policy (registry) for one browser. Null/empty = not set.</summary>
public sealed record BrowserPolicySettings(
    string? HomePage,
    string? SearchUrl,
    IReadOnlyList<string> StartupUrls,
    string? NewTabPage)
{
    public static BrowserPolicySettings None { get; } = new(null, null, [], null);

    public bool IsEmpty => HomePage is null && SearchUrl is null && StartupUrls.Count == 0 && NewTabPage is null;
}
