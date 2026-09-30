# 25 - Theme setting (branch `feat/theme-setting`)

Porchlight follows the Windows light/dark setting. Some users want to pin it either way, so the
Notifications dialog gets a small "Appearance" card with a Theme picker.

## Behaviour

- Choices: **Match Windows** (default), **Light**, **Dark** (`AppTheme.System/Light/Dark`).
- Stored as `Appearance.Theme` in `settings.json`, by name (`"System"`, `"Light"`, `"Dark"`). A
  settings file without the section loads with the default.
- Applied at startup after the host is built and before any window is created, and immediately on
  change, by setting `Application.ThemeMode` (Fluent theme). This propagates to every open window,
  including Setup and the Notifications dialog itself, and to windows opened later.
- `IThemeService`/`ThemeService` in `Porchlight.App.Shell` owns the mapping to `ThemeMode`.

## Non-goals

- No custom colours or per-window themes.
