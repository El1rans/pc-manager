namespace Porchlight.Core.Checkup;

/// <summary>Plain-language labels for <see cref="CheckupSeverity"/>, shared by every renderer so
/// status is always words, never colour alone.</summary>
public static class CheckupSeverityText
{
    public static string Label(CheckupSeverity severity) => severity switch
    {
        CheckupSeverity.Ok => "OK",
        CheckupSeverity.NeedsAttention => "Needs attention",
        _ => "Problem",
    };
}
