# 22 - Navigation categories (branch `feat/nav-categories`)

After milestones 12-19 the navigation rail held 11 pages and needed scrolling at the default window
size, so the last pages (Browser add-ons, Web console) were easy to miss. That's too cluttered for a
non-technical user. This milestone groups pages into a few categories: the rail shows only the
categories, and a category with more than one page shows a row of tabs above its content.

## Goals

- The rail shows 5 entries and never needs to scroll at the 900x600 minimum window size.
- Every existing page stays reachable in at most two clicks, keeps its own view, view model and
  `OnNavigatedToAsync` behaviour, and needs no change beyond declaring its category.
- There is one navigation path: code that opens a page (Dashboard's "Free up space" button, tray
  menu, alert balloons) selects both the right category and the right tab.

## Non-goals

- No change to any page's own content or layout. No collapsible tree, no hamburger menu, no search.
- Categories aren't user-configurable.

## Categories (v1)

| Order | Category | Glyph | Pages (tab order) |
|---|---|---|---|
| 0 | Overview | `` (Home) | Dashboard |
| 1 | Tune-up | `` (Repair) | Updates, Startup apps, Free up space, Health check |
| 2 | Internet & safety | `` (Globe) | Internet, Browser add-ons |
| 3 | Hardware | `` (TVMonitor) | Hardware, Lighting |
| pinned | Get help | (Get help's current glyph) | Get help, Web console |

The Hardware page's tab reads "Sensors & fans". Its rail/page `Title` stays "Hardware"; add an optional
`IPage.TabTitle` that defaults to `Title`.

## Design

- **Shell/`PageCategory`** (enum: `Overview`, `TuneUp`, `InternetAndSafety`, `Hardware`, `Help`) and
  **Shell/`PageCategoryCatalog`**: the single place mapping each category to its title, glyph, order and
  `IsPinnedToBottom` (only `Help`).
- **`IPage`** gains `PageCategory Category { get; }` (abstract on `PageViewModelBase`, so no page can
  silently land in the wrong group) and `string TabTitle { get; }` (virtual, defaults to `Title`).
  `IPage.Order` now means the tab order *within* the category. `IPage.IsPinnedToBottom` is removed
  and derived from the category instead. Update every page view model; web console and Get help both
  go in `Help`.
- **`NavCategoryViewModel`** (Shell): title, glyph, `Pages` (ordered), `SelectedPage` (the last tab
  used in this category, remembered for the session; starts at the first page), `HasTabs`
  (`Pages.Count > 1`), and `Badge`. The badge is the sum of numeric page badges; if a page's badge
  isn't numeric, the first non-empty one is used. It updates live through each page's
  `PropertyChanged` (so the Updates count shows on "Tune-up"), and the subscription is removed on
  dispose.
- **`MainViewModel`**: `Categories` (unpinned, ordered) + `PinnedCategories`, and `SelectedCategory`.
  `SelectedPage` stays the single source of truth for which view is shown and what's loaded.
  - Selecting a category selects its remembered page.
  - Selecting a tab sets that category's `SelectedPage` and `MainViewModel.SelectedPage`.
  - `OnNavigationRequested(Type)` finds the page, selects its category, and makes that page the tab.
  - `FindBusyPage` checks every page, pinned ones included.
  - `InitializeAsync` selects the first category.
- **One navigation path**: add a non-generic `IPageNavigator.NavigateTo(Type pageViewModelType)`. Then
  `ShellWindowService.NavigateTo` = show window + `IPageNavigator.NavigateTo(type)`. Delete
  `MainWindow.SelectPage`, so the tray, alerts and Dashboard all go through `MainViewModel`.
- **MainWindow.xaml**:
  - The two rail ListBoxes bind to `Categories` / `PinnedCategories`, keeping the existing OneWay
    `SelectedItem` + code-behind selection pattern (see the comments there - that pattern fixes a
    real bug and must be preserved) and the badge template.
  - The content area is a `DockPanel`. At the top is a tab strip (a horizontal `ListBox`, or a
    styled `TabControl` header without content, bound to `SelectedCategory.Pages` /
    `SelectedCategory.SelectedPage`, showing `TabTitle` plus each page's own badge), shown only when
    `SelectedCategory.HasTabs`. Below it is the existing `ContentControl`.
  - Tabs use Fluent brushes, a clear selected state (accent underline or pill + SemiBold, never
    color alone), keyboard navigation, and `AutomationProperties.Name` = `TabTitle`.
- Rail item text is the category title. The window's minimum size is unchanged.

## Acceptance criteria

- [ ] Rail shows exactly Overview, Tune-up, Internet & safety, Hardware, and pinned Get help; no
      scrollbar at 900x600.
- [ ] Tabs appear only for categories with more than one page; switching tabs loads that page exactly as
      navigating to it did before (its `OnNavigatedToAsync` runs, and in-flight loads are cancelled on
      switch).
- [ ] Re-selecting a category returns to the tab last used in it.
- [ ] The Updates badge count shows on "Tune-up" and on the Updates tab, and updates live.
- [ ] The Dashboard "Free up space" button, tray "Check for updates"/"Get help", and alert clicks land
      on the right category *and* tab. There's only one navigation implementation.
- [ ] Unit tests: grouping/ordering, badge aggregation (numeric sum, non-numeric fallback, live
      update, unsubscribe on dispose), navigation by type selecting category + tab, remembered tab,
      `FindBusyPage` including pinned pages.
- [ ] README "Features" nav description updated; `dotnet build -c Release` 0 warnings; tests pass.
