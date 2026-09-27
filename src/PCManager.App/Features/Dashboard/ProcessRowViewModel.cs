using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

/// <summary>One row of the "Top processes" card.</summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    public ProcessRowViewModel(ProcessGroupSnapshot snapshot)
    {
        NameText = snapshot.InstanceCount > 1 ? $"{snapshot.Name} ({snapshot.InstanceCount})" : snapshot.Name;
        CpuText = $"{snapshot.CpuPercent:0.0}%";
        MemoryText = ByteFormatter.FormatBytes(snapshot.WorkingSetBytes);
    }

    public string NameText { get; }

    public string CpuText { get; }

    public string MemoryText { get; }
}
