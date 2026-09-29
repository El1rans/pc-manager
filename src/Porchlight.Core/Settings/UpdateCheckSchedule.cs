using System.Text.Json.Serialization;

namespace Porchlight.Core.Settings;

/// <summary>How often Porchlight checks for app updates in the background. See
/// <c>docs/specs/15-tray-and-alerts.md</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateCheckSchedule
{
    /// <summary>Never check on a schedule (the user can still check by hand).</summary>
    Never,

    /// <summary>Check about once a day.</summary>
    Daily,

    /// <summary>Check about once a week.</summary>
    Weekly,
}
