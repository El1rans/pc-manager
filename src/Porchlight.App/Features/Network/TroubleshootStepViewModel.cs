using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Network;

namespace Porchlight.App.Features.Network;

/// <summary>One row of the "Fix my internet" checklist: a title, an icon and a plain word, so the
/// state is never shown by color alone.</summary>
public sealed partial class TroubleshootStepViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph), nameof(ResultText), nameof(IsPassed), nameof(IsProblem))]
    private StepState _state = StepState.Pending;

    public TroubleshootStepViewModel(TroubleshootStep step)
    {
        Step = step;
        Title = TroubleshootStepText.Title(step);
    }

    public TroubleshootStep Step { get; }

    public string Title { get; }

    public string ResultText => TroubleshootStepText.Describe(Step, State);

    public bool IsPassed => State == StepState.Passed;

    public bool IsProblem => State is StepState.Failed or StepState.NeedsSignIn;

    /// <summary>Segoe Fluent Icons glyph for the state.</summary>
    public string Glyph => State switch
    {
        StepState.Running => "",
        StepState.Passed => "",
        StepState.Failed => "",
        StepState.NeedsSignIn => "",
        StepState.Skipped => "",
        _ => "",
    };
}
