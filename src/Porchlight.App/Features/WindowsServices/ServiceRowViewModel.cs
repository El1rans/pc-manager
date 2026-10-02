using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.WindowsServices;

namespace Porchlight.App.Features.WindowsServices;

/// <summary>One row of the Services page.</summary>
public sealed partial class ServiceRowViewModel : ObservableObject
{
    private const string RunningGlyph = "";
    private const string StoppedGlyph = "";
    private const string BusyGlyph = "";
    private const string OtherGlyph = "";

    private readonly bool _isElevated;
    private readonly Func<ServiceRowViewModel, StartTypeOption, StartTypeOption, Task> _onStartTypeSelected;
    private bool _suppressSelection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange), nameof(CanStart), nameof(CanStop), nameof(CanRestart))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(StateGlyph), nameof(CanStart), nameof(CanStop), nameof(CanRestart))]
    private ServiceRunState _state;

    [ObservableProperty]
    private StartTypeOption? _selectedStartOption;

    public ServiceRowViewModel(
        WindowsServiceEntry entry,
        bool isElevated,
        Func<ServiceRowViewModel, StartTypeOption, StartTypeOption, Task> onStartTypeSelected)
    {
        Entry = entry;
        _isElevated = isElevated;
        _onStartTypeSelected = onStartTypeSelected;
        _state = entry.State;
        _selectedStartOption = StartTypeOption.For(entry.StartType);
    }

    public WindowsServiceEntry Entry { get; }

    /// <summary>The start-type change currently being applied (completed when none); lets tests await it.</summary>
    public Task PendingChange { get; private set; } = Task.CompletedTask;

    public string Name => Entry.Name;

    public string DisplayName => Entry.DisplayName;

    public string Description => string.IsNullOrWhiteSpace(Entry.Description) ? "No description" : Entry.Description;

    public string Publisher => string.IsNullOrWhiteSpace(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;

    public bool IsMicrosoft => Entry.IsMicrosoft;

    public bool IsChangeable => Entry.IsChangeable;

    public IReadOnlyList<StartTypeOption> StartTypeOptions { get; } = StartTypeOption.All;

    /// <summary>The plain-words start type, shown as text when the row is read-only.</summary>
    public string StartTypeText => ServiceClassifier.StartTypeLabel(Entry.StartType);

    public string StateText => ServiceClassifier.StateLabel(State);

    public string StateGlyph => State switch
    {
        ServiceRunState.Running => RunningGlyph,
        ServiceRunState.Stopped => StoppedGlyph,
        ServiceRunState.Starting or ServiceRunState.Stopping => BusyGlyph,
        _ => OtherGlyph,
    };

    public bool CanChange => IsChangeable && _isElevated && !IsBusy;

    public bool CanStart => CanChange && State != ServiceRunState.Running;

    public bool CanStop => CanChange && State == ServiceRunState.Running;

    public bool CanRestart => CanChange && State == ServiceRunState.Running;

    public bool ShowNeedsAdmin => IsChangeable && !_isElevated;

    public string StartAutomationName => $"Start {DisplayName}";

    public string StopAutomationName => $"Stop {DisplayName}";

    public string RestartAutomationName => $"Restart {DisplayName}";

    public string StartTypeAutomationName => $"Start type of {DisplayName}";

    /// <summary>Puts the combo back on <paramref name="option"/> without applying it again.</summary>
    public void RevertStartOption(StartTypeOption option)
    {
        _suppressSelection = true;
        try
        {
            SelectedStartOption = option;
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    partial void OnSelectedStartOptionChanged(StartTypeOption? oldValue, StartTypeOption? newValue)
    {
        if (_suppressSelection || oldValue is null || newValue is null || oldValue == newValue)
        {
            return;
        }

        // Applied on selection; the page view model reports the result and reverts on failure.
        PendingChange = _onStartTypeSelected(this, oldValue, newValue);
    }
}
