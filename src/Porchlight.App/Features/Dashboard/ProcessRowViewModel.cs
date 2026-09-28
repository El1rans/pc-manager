using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

/// <summary>One row of the "Top processes" card. Mutable (via <see cref="Apply"/>) so the
/// dashboard can update rows in place instead of clearing and recreating the list every tick.</summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    public ProcessRowViewModel(ProcessGroupSnapshot snapshot) => Apply(snapshot);

    [ObservableProperty]
    private string _nameText = string.Empty;

    [ObservableProperty]
    private string _cpuText = string.Empty;

    [ObservableProperty]
    private string _memoryText = string.Empty;

    public void Apply(ProcessGroupSnapshot snapshot)
    {
        NameText = snapshot.InstanceCount > 1 ? $"{snapshot.Name} ({snapshot.InstanceCount})" : snapshot.Name;
        CpuText = $"{snapshot.CpuPercent:0.0}%";
        MemoryText = ByteFormatter.FormatBytes(snapshot.WorkingSetBytes);
    }
}
