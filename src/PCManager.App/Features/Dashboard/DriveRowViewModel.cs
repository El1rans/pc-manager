using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

/// <summary>One row of the "Drives" card.</summary>
public sealed partial class DriveRowViewModel : ObservableObject
{
    public DriveRowViewModel(DriveSnapshot snapshot)
    {
        Name = string.IsNullOrEmpty(snapshot.Label) ? snapshot.Name : $"{snapshot.Name} ({snapshot.Label})";
        DetailText = $"{ByteFormatter.FormatBytes(snapshot.FreeBytes)} free of {ByteFormatter.FormatBytes(snapshot.TotalBytes)}";
        UsedPercent = snapshot.TotalBytes <= 0
            ? 0
            : Math.Clamp(100.0 * (snapshot.TotalBytes - snapshot.FreeBytes) / snapshot.TotalBytes, 0, 100);
        IsLow = snapshot.IsLow;
    }

    public string Name { get; }

    public string DetailText { get; }

    public double UsedPercent { get; }

    public bool IsLow { get; }
}
