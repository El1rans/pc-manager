# 40 - Vivid colour (branch `feat/vivid-color`)

Porchlight looked monotone: every tile, bar and badge used the Windows accent colour, which on many
PCs is grey or default blue, so nothing on a page stood out. Four levels were mocked up (current,
brand accent, colour-coded, vivid) and level 3, "vivid", was chosen.

## Goals

- **Brand accent.** Porchlight's lamp amber (from the icon) replaces the Windows accent everywhere
  Fluent uses it: accent buttons, check boxes, toggles, radio buttons, sliders, progress bars, focus,
  badges, the selected-page bar and tab underline.
- **Category hues.** Seven hues (`Controls/Hue.cs`): Amber, Blue, Violet, Green, Teal, Coral and
  Neutral, each with a light and a dark value tuned for contrast on that theme's cards.
  - Left menu: each category's icon sits on a rounded square in its hue (Overview amber, Tune-up
    green, Apps & services violet, Internet & safety blue, Hardware coral, Get help teal, Settings
    neutral). The selected row is tinted amber and keeps its accent bar.
  - Dashboard tiles: CPU blue, Memory violet, GPU green, Disk teal, Download amber, Upload coral. Each
    tile has an icon chip, its value in the hue, a filled sparkline with a dot on the newest sample,
    a tinted border and a wash that fades across the card.
- **Status colour.** Drive bars are green, amber from 85% used, red when Windows reports low space.
  Each Top processes row has a letter tile in a colour picked from the app's name (stable across
  runs, and the same in the web console) and a small bar of its memory relative to the largest row.
- **Glow.** A soft amber radial glow behind every page's title.
- **Web console.** The same hues, tile treatment, letter tiles, drive colours and glow; section
  buttons get their own hue, and security cards are tinted by verdict.
- Colour is never the only cue: titles, "Low space"/"Almost full", level labels and the selected-page
  bar stay as they were.

## Non-goals

- Tinted cards on every page. Other pages get the accent, menu colours and glow; per-page hues can
  follow page by page.
- A setting to turn colour off. High-contrast themes already drop the palette (below).

## Design

- `Themes/Palette.Dark.xaml` / `Palette.Light.xaml` hold the amber accent ramp
  (`SystemAccentColor*`) and, per hue, `Hue{Name}Brush`, `TintBrush`, `StrokeBrush` and `WashBrush`
  plus `BrandGlowBrush`. Both files must define the same keys (tested).
- `Shell/ThemePalette` (called by `ThemeService.Apply`) copies the palette for the *effective* theme
  into `Application.Resources`, where it wins over Fluent's merged dictionary. "System" reads
  `AppsUseLightTheme` and re-applies on `SystemEvents.UserPreferenceChanged`. Fluent bakes the Windows
  accent into its control brushes when it loads, so `ThemePalette` also finds every Fluent brush whose
  colour is one of the Windows accent shades and re-creates it in the amber shade of the same rank,
  keeping its alpha. In high contrast all of this is removed.
- `Controls/HueBrushes` is an attached property: `c:HueBrushes.Hue="Blue"` points inherited
  `Brush`/`Tint`/`Stroke`/`Wash` properties at that hue's resources as dynamic references, so they
  follow theme switches. `PaintBorder`/`PaintWash` (set by the `HueCard`/`HueWash` styles) paint a
  Border directly, because a style binding to those properties failed to resolve on the card itself.
- The letter-tile hash is shared by `ProcessRowViewModel.HueFor` and `app.js hueFor` (tested against
  known values).

## Tests

- `ThemePaletteTests`: effective dark/light per setting; both palettes define the same keys; every
  hue has all four brushes.
- `DashboardRowViewModelTests`: drive fill levels, process initials, stable hue, memory shares.
