using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Controls;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

/// <summary>One row of the "Top processes" card. Mutable (via <see cref="Apply"/>) so the
/// dashboard can update rows in place instead of clearing and recreating the list every tick.</summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    /// <summary>Colours an app's letter tile can take; picked by name so an app keeps its colour.</summary>
    private static readonly Hue[] LetterHues = [Hue.Blue, Hue.Violet, Hue.Green, Hue.Teal, Hue.Coral, Hue.Amber];

    public ProcessRowViewModel(ProcessGroupSnapshot snapshot) => Apply(snapshot);

    [ObservableProperty]
    private string _nameText = string.Empty;

    [ObservableProperty]
    private string _cpuText = string.Empty;

    [ObservableProperty]
    private string _memoryText = string.Empty;

    /// <summary>First letter or digit of the name, for the coloured letter tile.</summary>
    [ObservableProperty]
    private string _initial = string.Empty;

    [ObservableProperty]
    private Hue _hue;

    /// <summary>This row's memory as a share (0-1) of the largest row's, for the mini bar.</summary>
    [ObservableProperty]
    private double _memoryShare;

    public long WorkingSetBytes { get; private set; }

    public void Apply(ProcessGroupSnapshot snapshot)
    {
        NameText = snapshot.InstanceCount > 1 ? $"{snapshot.Name} ({snapshot.InstanceCount})" : snapshot.Name;
        CpuText = $"{snapshot.CpuPercent:0.0}%";
        MemoryText = ByteFormatter.FormatBytes(snapshot.WorkingSetBytes);
        WorkingSetBytes = snapshot.WorkingSetBytes;
        var initial = snapshot.Name.FirstOrDefault(char.IsLetterOrDigit);
        Initial = initial == default ? "?" : char.ToUpperInvariant(initial).ToString();
        Hue = HueFor(snapshot.Name);
    }

    /// <summary>Sets every row's <see cref="MemoryShare"/> relative to the largest row.</summary>
    public static void UpdateMemoryShares(IEnumerable<ProcessRowViewModel> rows)
    {
        var list = rows as IReadOnlyCollection<ProcessRowViewModel> ?? rows.ToList();
        var max = list.Count == 0 ? 0 : list.Max(r => r.WorkingSetBytes);
        foreach (var row in list)
        {
            row.MemoryShare = max <= 0 ? 0 : (double)row.WorkingSetBytes / max;
        }
    }

    /// <summary>A stable colour per name (not <see cref="string.GetHashCode()"/>, which changes every run).</summary>
    public static Hue HueFor(string name)
    {
        var sum = 0;
        foreach (var ch in name.ToUpperInvariant())
        {
            sum = unchecked((sum * 31) + ch);
        }

        return LetterHues[(int)((uint)sum % LetterHues.Length)];
    }
}
