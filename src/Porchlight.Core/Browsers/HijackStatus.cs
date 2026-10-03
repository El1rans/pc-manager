namespace Porchlight.Core.Browsers;

/// <summary>Plain-language verdict for one browser setting. Advice only, never a malware verdict.</summary>
public enum HijackStatus
{
    /// <summary>A well-known provider, the browser's own page, or the browser default.</summary>
    Ok,

    /// <summary>Points somewhere unfamiliar. Fine if the user chose it.</summary>
    Changed,

    /// <summary>Set by a policy on this PC and points somewhere unfamiliar (the strongest warning).</summary>
    ForcedByPolicy,
}
