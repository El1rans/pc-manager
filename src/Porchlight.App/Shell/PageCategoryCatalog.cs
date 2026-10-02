namespace Porchlight.App.Shell;

/// <summary>The single place mapping each <see cref="PageCategory"/> to its title, glyph, order and
/// pinning. See docs/specs/22-nav-categories.md.</summary>
public static class PageCategoryCatalog
{
    // Segoe Fluent Icons: Home, Repair, Globe, TVMonitor, People, Settings.
    private const string HomeGlyph = "";
    private const string RepairGlyph = "";
    private const string GlobeGlyph = "";
    private const string TvMonitorGlyph = "";
    private const string PeopleGlyph = "";
    private const string SettingsGlyph = "";

    private static readonly IReadOnlyDictionary<PageCategory, PageCategoryInfo> Infos =
        new Dictionary<PageCategory, PageCategoryInfo>
        {
            [PageCategory.Overview] = new(PageCategory.Overview, "Overview", HomeGlyph, 0, false),
            [PageCategory.TuneUp] = new(PageCategory.TuneUp, "Tune-up", RepairGlyph, 1, false),
            [PageCategory.InternetAndSafety] = new(PageCategory.InternetAndSafety, "Internet & safety", GlobeGlyph, 2, false),
            [PageCategory.Hardware] = new(PageCategory.Hardware, "Hardware", TvMonitorGlyph, 3, false),
            [PageCategory.Help] = new(PageCategory.Help, "Get help", PeopleGlyph, 4, true),
            [PageCategory.Settings] = new(PageCategory.Settings, "Settings", SettingsGlyph, 5, true),
        };

    /// <summary>Every category, unordered.</summary>
    public static IEnumerable<PageCategoryInfo> All => Infos.Values;

    public static PageCategoryInfo Get(PageCategory category) => Infos[category];
}
