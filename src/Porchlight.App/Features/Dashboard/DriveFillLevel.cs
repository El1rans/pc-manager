namespace Porchlight.App.Features.Dashboard;

/// <summary>How full a drive is, for the colour of its bar on the Dashboard.</summary>
public enum DriveFillLevel
{
    /// <summary>Plenty of room: green.</summary>
    Normal,

    /// <summary>At least <see cref="DriveRowViewModel.FillingPercent"/> used: amber.</summary>
    Filling,

    /// <summary>Windows' own "low space" state: red (the row also says "Low space").</summary>
    Low,
}
