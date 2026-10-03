namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the "Recent changes" feature (spec 34).</summary>
public sealed class ChangesSettings
{
    /// <summary>"Create a restore point before big changes": before a batch of app updates or a
    /// service start-type change Porchlight asks Windows for a restore point, when it can.</summary>
    public bool CreateRestorePointBeforeBigChanges { get; set; } = true;
}
