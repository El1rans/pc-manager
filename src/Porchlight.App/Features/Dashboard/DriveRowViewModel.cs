using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

/// <summary>One row of the "Drives" card. Mutable (via <see cref="Apply"/>) so the dashboard can
/// update rows in place instead of clearing and recreating the list every sample.</summary>
public sealed partial class DriveRowViewModel : ObservableObject
{
    /// <summary>Used share, in percent, from which a drive that is not yet "low" shows as
    /// <see cref="DriveFillLevel.Filling"/>.</summary>
    public const double FillingPercent = 85;

    public DriveRowViewModel(DriveSnapshot snapshot) => Apply(snapshot);

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private double _usedPercent;

    [ObservableProperty]
    private bool _isLow;

    [ObservableProperty]
    private DriveFillLevel _fillLevel;

    public void Apply(DriveSnapshot snapshot)
    {
        Name = string.IsNullOrEmpty(snapshot.Label) ? snapshot.Name : $"{snapshot.Name} ({snapshot.Label})";
        DetailText = $"{ByteFormatter.FormatBytes(snapshot.FreeBytes)} free of {ByteFormatter.FormatBytes(snapshot.TotalBytes)}";
        UsedPercent = snapshot.TotalBytes <= 0
            ? 0
            : Math.Clamp(100.0 * (snapshot.TotalBytes - snapshot.FreeBytes) / snapshot.TotalBytes, 0, 100);
        IsLow = snapshot.IsLow;
        FillLevel = snapshot.IsLow ? DriveFillLevel.Low
            : UsedPercent >= FillingPercent ? DriveFillLevel.Filling
            : DriveFillLevel.Normal;
    }
}
