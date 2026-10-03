# 37 - Larger text (branch `feat/larger-text`)

Some users find Porchlight's text small. Settings > General gets a "Text size" card that makes the
text and pictures on each page bigger, live, without a restart.

## Goals

- Choices: **Normal** (default, 100%), **Large** (125%), **Extra large** (150%) -
  `TextSize.Normal/Large/ExtraLarge`, factors are the named constants in `TextSizeScale`.
- Stored as `Appearance.TextSize` in `settings.json`, by name. A file without it loads as Normal.
- Applied at startup (next to the theme, before any window exists) and immediately on change.
- The page area scales; the nav rail, the tab strip above a page, the tray menu and toasts do not.
- Every page stays usable at 900x600: content reflows to the smaller virtual width and scrolls
  vertically instead of clipping.

## Non-goals

- Scaling the nav rail / tab strip (they are navigation and must stay reachable), the tray menu
  or toast notifications (owned by Windows).
- Per-page sizes, or a free-form percentage.
- Fixing individual wide elements that were already fixed-width (see "Known limits").

## Design

### Approach chosen: a layout transform on the page area (option a)

Two options were weighed:

- **(b) font-size resources.** Text-only, layout reflows. Rejected: the app has about 100
  hard-coded `FontSize` values across 16 XAML files (plus the Fluent control templates read their
  own size keys), so making them all dynamic means editing nearly every page and the shared
  `Styles.xaml`, which other work touches in parallel, and any missed spot would silently stay
  small. Icons, charts and sparklines would also stay small next to bigger text.
- **(a) `LayoutTransform` + `ScaleTransform` on the page area.** One place, consistent, scales
  text, icons, charts and spacing together. A `LayoutTransform` (unlike a `RenderTransform`)
  makes the child lay out in the shrunken virtual space and then scales the result, so text wraps
  and each page's own `ScrollViewer` scrolls; nothing is simply cropped. Chosen.

### Pieces

- `Porchlight.Core.Settings.TextSize` (enum) and `TextSizeScale.FactorFor` (mapping).
- `AppearanceSettings.TextSize` (default Normal).
- `ITextScaleService` / `TextScaleService` in `Porchlight.App.Shell`, same pattern as
  `IThemeService`: `Apply(TextSize)` puts a frozen `ScaleTransform` in the application resources
  under `PageTextScaleTransform`. `TextScaleService.Apply(size, ResourceDictionary)` is the
  testable core (no window or Application needed).
- `MainWindow.xaml`: a `Border` around the page `ContentControl` (not the tab strip) with
  `LayoutTransform="{DynamicResource PageTextScaleTransform}"`. Because it is a `DynamicResource`,
  changing the resource re-lays out the open page immediately.
- `TextSizeViewModel` + `TextSizeCard` (own card on the General page, resolved from the container
  like `AdminRequiredBanner`, so the General view model is untouched): writes via
  `ISettingsStore.Update` and calls `ITextScaleService.Apply`.
- Real registrations are in `SettingsFeature` (not in the DEBUG demo branch); `App.OnStartup`
  applies the saved size right after the theme.

### Windows' own "Text size" setting

WPF already honours Windows' Settings > Accessibility > Text size for system fonts / Fluent
controls, and Porchlight's hard-coded sizes are not affected by it. Our setting is independent and
stacks on top of that (the transform scales whatever WPF produces); no extra code.

### Known limits (observed at 900x600)

- The big address on Get help, and the large numbers on dashboard tiles (e.g. "103.2 Mbps"),
  are fixed-width elements that were already trimmed at Normal on narrow tiles; at Extra large
  they trim slightly sooner.
- The wider the page, the earlier it scrolls vertically; none needed horizontal scrolling.

## Tests

- `SettingsStoreTests`: default Normal when missing from an old file; round-trips by name.
- `TextScaleServiceTests`: each size writes a `ScaleTransform` with the mapped factor into a
  plain `ResourceDictionary`; a later call replaces the earlier one; factors strictly increase.
- `TextSizeViewModelTests`: default Normal and three labelled options; saved value selected on
  open without writing or applying; choosing persists via `Update` and applies immediately.
- `AppCompositionTests` (existing) validates the real container with the new registrations.

## Acceptance criteria

- [ ] Settings > General shows "Text size" with Normal / Large / Extra large.
- [ ] Choosing a size changes every page's text and pictures live; the nav rail and tab strip
      stay the same size.
- [ ] The choice survives a restart.
- [ ] At 900x600 and Extra large, each page scrolls vertically instead of clipping; every tab
      remains reachable.
- [ ] Build has no warnings; tests pass.
