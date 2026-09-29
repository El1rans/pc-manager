using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>The Battery card - stays hidden (<see cref="IsVisible"/> false) on a device with no battery.</summary>
public sealed partial class BatteryCardViewModel(IBatteryService service) : HealthCardViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool _hasBattery;

    [ObservableProperty]
    private string _verdictText = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private string _glyph = HealthGlyphs.Unknown;

    [ObservableProperty]
    private HealthSeverity _severity;

    /// <summary>Shown when there is a battery, or when checking for one failed (so the failure isn't silent).</summary>
    public bool IsVisible => HasBattery || ShowError;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        try
        {
            var result = await service.GetAsync(cancellationToken);
            if (!result.Succeeded)
            {
                HasBattery = false;
                SetFailure(result.Error);
                OnPropertyChanged(nameof(IsVisible));
                return;
            }

            ErrorText = null;
            if (result.Value is not { } reading)
            {
                HasBattery = false;
                OnPropertyChanged(nameof(IsVisible));
                return;
            }

            var assessment = BatteryHealthCalculator.Assess(reading);
            VerdictText = assessment.Text;
            Severity = assessment.Verdict switch
            {
                BatteryVerdict.Good => HealthSeverity.Ok,
                BatteryVerdict.Unknown => HealthSeverity.Neutral,
                _ => HealthSeverity.Warning,
            };
            Glyph = HealthGlyphs.For(Severity);

            var parts = new List<string>();
            if (assessment.HealthPercent is { } percent)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"Holds {percent}% of its original charge"));
            }

            if (reading.ChargePercent is { } charge)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{charge}% charged now"));
            }

            parts.Add(BatteryHealthCalculator.DescribeState(reading.State));
            if (reading.CycleCount is { } cycles)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{cycles} charge cycles"));
            }

            DetailText = string.Join(" - ", parts);
            HasBattery = true;
            OnPropertyChanged(nameof(IsVisible));
        }
        finally
        {
            IsChecking = false;
        }
    }
}
