using PCManager.Core.Hardware;

namespace PCManager.Core.Settings;

/// <summary>Settings owned by the Hardware feature: software fan control's master switch and every
/// fan's profile. Fan control is OFF by default (spec 04) - a fresh <see cref="AppSettings"/>
/// leaves <see cref="FanControlEnabled"/> false and every fan un-profiled (BIOS/Default control).</summary>
public sealed class HardwareSettings
{
    /// <summary>User's persisted intent to use software fan control. This alone never causes control
    /// to start - see <see cref="FanControlWarningConfirmed"/> and the App-level rule that control
    /// only resumes after the Hardware page has loaded successfully (spec 04).</summary>
    public bool FanControlEnabled { get; set; }

    /// <summary>Whether the user has clicked through the one-time risk warning dialog. Reset to
    /// false is never done automatically; once confirmed, re-enabling the master switch later does
    /// not show the dialog again.</summary>
    public bool FanControlWarningConfirmed { get; set; }

    /// <summary>Rule 1 floor, 20-100. Defaults to <see cref="FanControlOptions.DefaultMinPercent"/>.</summary>
    public int MinFanPercent { get; set; } = FanControlOptions.DefaultMinPercent;

    /// <summary>Rule 2 threshold, 70-95 C. Defaults to
    /// <see cref="FanControlOptions.DefaultFailsafeTemperatureC"/>.</summary>
    public double FailsafeTemperatureC { get; set; } = FanControlOptions.DefaultFailsafeTemperatureC;

    /// <summary>Per-fan profile, keyed by <see cref="IFanController.Id"/>. A fan with no entry here
    /// is treated as <see cref="FanMode.Default"/>.</summary>
    public Dictionary<string, FanProfileSettings> FanProfiles { get; set; } = [];
}

/// <summary>Persisted form of a fan's profile - <see cref="FanCurvePoint"/> is reused directly since
/// it is already a plain, JSON-friendly record.</summary>
public sealed class FanProfileSettings
{
    public FanMode Mode { get; set; } = FanMode.Default;

    public double FixedPercent { get; set; }

    public string? SourceSensorId { get; set; }

    public List<FanCurvePoint> CurvePoints { get; set; } = [];
}
