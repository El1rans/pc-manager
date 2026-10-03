using System.Windows.Input;
using Porchlight.Core.Browsers;

namespace Porchlight.App.Features.Browsers;

/// <summary>One browser's group on the page: its name, its add-ons and the "open add-ons page" button.</summary>
public sealed class BrowserSectionViewModel
{
    public BrowserSectionViewModel(
        BrowserKind browser,
        IReadOnlyList<ExtensionItemViewModel> items,
        int totalCount,
        ICommand openCommand,
        IReadOnlyList<HijackRowViewModel>? hijackRows = null)
    {
        HijackRows = hijackRows ?? [];
        Browser = browser;
        Items = items;
        TotalCount = totalCount;
        OpenCommand = openCommand;
    }

    public BrowserKind Browser { get; }

    public string Name => DisplayName(Browser);

    public IReadOnlyList<ExtensionItemViewModel> Items { get; }

    public int TotalCount { get; }

    public ICommand OpenCommand { get; }

    /// <summary>The start-up, home page, new-tab and search checks for this browser (spec 32).</summary>
    public IReadOnlyList<HijackRowViewModel> HijackRows { get; }

    public bool HasHijackRows => HijackRows.Count > 0;

    public string OpenButtonText => $"Open {Name}'s add-ons page";

    public string EmptyText => TotalCount == 0
        ? "No add-ons found in this browser."
        : "Nothing here needs a second look.";

    public bool ShowEmptyText => Items.Count == 0;

    public static string DisplayName(BrowserKind browser) => browser switch
    {
        BrowserKind.Edge => "Microsoft Edge",
        BrowserKind.Chrome => "Google Chrome",
        BrowserKind.Brave => "Brave",
        BrowserKind.Firefox => "Firefox",
        _ => browser.ToString(),
    };
}
