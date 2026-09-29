namespace Porchlight.Core.Alerts;

/// <summary>The kinds of background alert Porchlight can raise.</summary>
public enum AlertKind
{
    LowDisk,
    HighTemperature,
    UpdatesAvailable,
    RestartPending,
}
