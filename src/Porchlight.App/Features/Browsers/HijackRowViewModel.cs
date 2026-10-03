using System.Windows.Input;
using Porchlight.Core.Browsers;

namespace Porchlight.App.Features.Browsers;

/// <summary>Display model for one checked setting (home page, search engine, ...) of a browser. Immutable.</summary>
public sealed class HijackRowViewModel
{
    private const string OkGlyph = "";
    private const string ChangedGlyph = "";
    private const string ForcedGlyph = "";

    private readonly BrowserKind _browser;

    public HijackRowViewModel(HijackFinding finding, ICommand openCommand)
    {
        ArgumentNullException.ThrowIfNull(finding);
        _browser = finding.Browser;
        Status = finding.Status;
        SettingText = SettingName(finding.Setting);
        Details = finding.ProfileName is { Length: > 0 } profile
            ? $"{finding.Value}  |  Profile: {profile}"
            : finding.Value;
        OpenCommand = openCommand;

        (StatusText, StatusGlyph, Advice) = finding.Status switch
        {
            HijackStatus.ForcedByPolicy => (
                "Forced by a setting on this PC",
                ForcedGlyph,
                "A setting on this PC is forcing this, which is unusual on a home computer. "
                + "If nobody set this up for you, ask the family member who looks after your PC."),
            HijackStatus.Changed => (
                "Changed",
                ChangedGlyph,
                "If you didn't choose this, reset it in the browser's settings."),
            _ => ("Looks fine", OkGlyph, string.Empty),
        };
    }

    public HijackStatus Status { get; }

    public string SettingText { get; }

    public string Details { get; }

    public string StatusText { get; }

    public string StatusGlyph { get; }

    public string Advice { get; }

    public bool HasAdvice => Advice.Length > 0;

    public ICommand OpenCommand { get; }

    public string OpenButtonText => $"Open {BrowserSectionViewModel.DisplayName(_browser)}'s settings";

    public bool NeedsAttention => Status != HijackStatus.Ok;

    public string AutomationName => $"{OpenButtonText} to check {SettingText}";

    public static string SettingName(HijackSetting setting) => setting switch
    {
        HijackSetting.HomePage => "Home page",
        HijackSetting.StartupPages => "Pages that open at start-up",
        HijackSetting.NewTabPage => "New tab page",
        HijackSetting.SearchEngine => "Search engine",
        _ => setting.ToString(),
    };
}
