using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

/// <summary>One row of the "Drives" card. Mutable (via <see cref="Apply"/>) so the dashboard can
/// update rows in place instead of clearing and recreating the list every sample.</summary>
public sealed partial class DriveRowViewModel : ObservableObject
{
    public DriveRowViewModel(DriveSnapshot snapshot) => Apply(snapshot);

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private double _usedPercent;

    [ObservableProperty]
    private bool _isLow;

    public void Apply(DriveSnapshot snapshot)
    {
        Name = string.IsNullOrEmpty(snapshot.Label) ? snapshot.Name : $"{snapshot.Name} ({snapshot.Label})";
        DetailText = $"{ByteFormatter.FormatBytes(snapshot.FreeBytes)} free of {ByteFormatter.FormatBytes(snapshot.TotalBytes)}";
        UsedPercent = snapshot.TotalBytes <= 0
            ? 0
            : Math.Clamp(100.0 * (snapshot.TotalBytes - snapshot.FreeBytes) / snapshot.TotalBytes, 0, 100);
        IsLow = snapshot.IsLow;
    }
}
