using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Components;

namespace PCManager.App.Features.Setup;

/// <summary>One row of the first-run setup list: a component the user can choose to set up.</summary>
public sealed partial class SetupItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ComponentStatus _status = ComponentStatus.NotInstalled;

    [ObservableProperty]
    private string? _progressMessage;

    public SetupItemViewModel(ComponentDefinition definition)
    {
        Definition = definition;
    }

    public ComponentDefinition Definition { get; }

    public string DisplayName => Definition.DisplayName;

    public string Purpose => Definition.Purpose;

    public bool IsAlreadyInstalled => Status.State is ComponentState.Installed or ComponentState.Running;

    public string StatusText => Status.State switch
    {
        ComponentState.Installed or ComponentState.Running => "Already installed",
        ComponentState.Error => Status.Message ?? "Setup failed.",
        _ => ProgressMessage ?? string.Empty,
    };

    partial void OnStatusChanged(ComponentStatus value) => OnPropertyChanged(nameof(StatusText));

    partial void OnProgressMessageChanged(string? value) => OnPropertyChanged(nameof(StatusText));
}
