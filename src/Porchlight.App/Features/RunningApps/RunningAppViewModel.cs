using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Monitoring;
using Porchlight.Core.RunningApps;

namespace Porchlight.App.Features.RunningApps;

/// <summary>One row of the Running apps page. Updated in place by <see cref="Apply"/> so the list
/// is never rebuilt (that would lose scroll position, selection and keyboard focus).</summary>
public sealed partial class RunningAppViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _cpuText = string.Empty;

    [ObservableProperty]
    private string _memoryText = string.Empty;

    [ObservableProperty]
    private string? _executablePath;

    [ObservableProperty]
    private bool _canEnd;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private double _cpuPercent;

    [ObservableProperty]
    private long _memoryBytes;

    [ObservableProperty]
    private RunningAppSection _section;

    public RunningAppViewModel(RunningApp app)
    {
        Key = app.Key;
        Apply(app);
    }

    public string Key { get; }

    public bool HasPath => !string.IsNullOrEmpty(ExecutablePath);

    public bool CannotEnd => !CanEnd;

    public string EndAutomationName => $"End task {Name}";

    public string OpenLocationAutomationName => $"Open file location of {Name}";

    public string RowAutomationName => $"{DisplayName}, CPU {CpuText}, memory {MemoryText}";

    public void Apply(RunningApp app)
    {
        Name = app.Name;
        DisplayName = app.ProcessCount > 1 ? $"{app.Name} ({app.ProcessCount})" : app.Name;
        ProcessCount = app.ProcessCount;
        CpuPercent = app.CpuPercent;
        MemoryBytes = app.MemoryBytes;
        CpuText = app.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        MemoryText = ByteFormatter.FormatBytes(app.MemoryBytes);
        ExecutablePath = app.ExecutablePath;
        CanEnd = app.CanEnd;
        Section = app.Section;
        OnPropertyChanged(nameof(HasPath));
        OnPropertyChanged(nameof(CannotEnd));
        OnPropertyChanged(nameof(EndAutomationName));
        OnPropertyChanged(nameof(OpenLocationAutomationName));
        OnPropertyChanged(nameof(RowAutomationName));
    }
}
