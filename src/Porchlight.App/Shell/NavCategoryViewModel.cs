using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Porchlight.App.Shell;

/// <summary>One navigation-rail entry: a category and its pages (shown as tabs when there is more
/// than one). Remembers the last tab used and aggregates the pages' badges.</summary>
public sealed partial class NavCategoryViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    [ObservableProperty]
    private IPage _selectedPage;

    [ObservableProperty]
    private string? _badge;

    public NavCategoryViewModel(PageCategoryInfo info, IEnumerable<IPage> pages)
    {
        Info = info;
        Pages = new ObservableCollection<IPage>(pages.OrderBy(p => p.Order));
        if (Pages.Count == 0)
        {
            throw new ArgumentException("A category needs at least one page.", nameof(pages));
        }

        _selectedPage = Pages[0];
        foreach (var page in Pages)
        {
            page.PropertyChanged += OnPagePropertyChanged;
        }

        _badge = ComputeBadge();
    }

    public PageCategoryInfo Info { get; }

    public PageCategory Category => Info.Category;

    public string Title => Info.Title;

    public string Glyph => Info.Glyph;

    public Controls.Hue Hue => Info.Hue;

    public bool IsPinnedToBottom => Info.IsPinnedToBottom;

    public ObservableCollection<IPage> Pages { get; }

    public bool HasTabs => Pages.Count > 1;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var page in Pages)
        {
            page.PropertyChanged -= OnPagePropertyChanged;
        }
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IPage.Badge) or null or "")
        {
            Badge = ComputeBadge();
        }
    }

    /// <summary>Sum of numeric page badges; if any page's badge isn't numeric, the first non-empty
    /// badge is used instead.</summary>
    private string? ComputeBadge()
    {
        var badges = Pages.Select(p => p.Badge).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        if (badges.Count == 0)
        {
            return null;
        }

        var sum = 0;
        foreach (var badge in badges)
        {
            if (!int.TryParse(badge, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return badges[0];
            }

            sum += value;
        }

        return sum.ToString(CultureInfo.InvariantCulture);
    }
}
