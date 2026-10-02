using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.RunningApps;

namespace Porchlight.App.Features.RunningApps;

/// <summary>One section (Apps, Background or Windows) of the Running apps page.</summary>
public sealed partial class RunningAppSectionViewModel : ObservableObject
{
    private readonly string _title;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _headerText;

    public RunningAppSectionViewModel(RunningAppSection section, string title, bool isExpanded)
    {
        Section = section;
        _title = title;
        _isExpanded = isExpanded;
        _headerText = title;
    }

    public RunningAppSection Section { get; }

    /// <summary>The rows shown, already filtered and sorted. Rows are moved, never recreated.</summary>
    public ObservableCollection<RunningAppViewModel> Items { get; } = [];

    public void UpdateHeader() => HeaderText = $"{_title} ({Items.Count})";
}
