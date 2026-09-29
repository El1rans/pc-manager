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
        ICommand openCommand)
    {
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
