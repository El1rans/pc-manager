using CommunityToolkit.Mvvm.ComponentModel;

namespace Porchlight.App.Features.Health;

/// <summary>Shared "checking" and "Couldn't check" state for a Health card.</summary>
public abstract partial class HealthCardViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError))]
    private string? _errorText;

    public bool ShowError => !string.IsNullOrEmpty(ErrorText);

    protected void SetFailure(string? reason) =>
        ErrorText = string.IsNullOrWhiteSpace(reason) ? "Couldn't check." : $"Couldn't check. {reason}";
}
