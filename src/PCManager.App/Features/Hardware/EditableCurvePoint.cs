using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>Mutable, bindable form of a <see cref="FanCurvePoint"/> for the curve editor - the
/// editor drags these directly; <see cref="FanCardViewModel"/> converts to/from the immutable
/// record when it persists or loads a profile.</summary>
public sealed partial class EditableCurvePoint : ObservableObject
{
    [ObservableProperty]
    private double _temperatureC;

    [ObservableProperty]
    private double _percent;

    public EditableCurvePoint(double temperatureC, double percent)
    {
        _temperatureC = temperatureC;
        _percent = percent;
    }

    public EditableCurvePoint(FanCurvePoint point)
        : this(point.TemperatureC, point.Percent)
    {
    }

    public FanCurvePoint ToPoint() => new(TemperatureC, Percent);
}
