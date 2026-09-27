namespace PCManager.Core.Settings;

/// <summary>Settings owned by the Updates feature.</summary>
public sealed class UpdatesSettings
{
    /// <summary>Winget package ids the user never wants offered for update.</summary>
    public List<string> IgnoredIds { get; set; } = [];

    /// <summary>"Silent install (hide installer windows)" toggle.</summary>
    public bool Silent { get; set; }

    /// <summary>"Include apps with unknown version" toggle.</summary>
    public bool IncludeUnknown { get; set; } = true;
}
