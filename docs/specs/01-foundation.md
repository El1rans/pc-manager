# 01 - Foundation (branch `chore/foundation`)

Goal: an empty but production-shaped app that every later feature plugs into without touching shared files beyond one DI line and one nav entry.

## Scope

1. **Solution and build config** exactly as laid out in `00-engineering-standards.md`: `Porchlight.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` (C# conventions: file-scoped namespaces, `var` when type is apparent, `_camelCase` private fields, braces required for multi-line blocks, 4-space indent, CRLF), `.gitattributes` (`* text=auto`, `*.cs`/`*.xaml` `eol=crlf`).
2. **Projects**: `src/Porchlight.Core`, `src/Porchlight.App`, `tests/Porchlight.Core.Tests`. A draft WPF shell already exists in `src/Porchlight/` (App.xaml, Styles.xaml, Sparkline control, BoolToVisibilityConverter, csproj, app.manifest). Move what is useful into `src/Porchlight.App` and delete `src/Porchlight/`.
3. **Host + DI**: `App.OnStartup` builds a generic host (`Host.CreateApplicationBuilder`), registers Serilog, `ISettingsStore`, the shell, and each feature via `Add<Feature>Feature()` extension methods. `OnExit` stops and disposes the host. Unhandled exceptions (dispatcher, `AppDomain`, `TaskScheduler.UnobservedTaskException`) are logged; dispatcher ones also show a friendly message box and are marked handled.
4. **Settings**: `ISettingsStore` in Core with `Load()`/`Save()` of an `AppSettings` record-like class. Atomic writes. Unknown/missing fields fall back to defaults. Corrupt file: log, back it up as `settings.json.bak`, use defaults. Each feature owns a nested settings section object (`Updates`, `Hardware`, `Lighting`) - create empty section classes now.
5. **Shell**: `MainWindow` with a left navigation rail (220px) and a content area. Nav items: Dashboard, Updates, Hardware, Lighting. Each nav item has title, Segoe Fluent icon glyph, and an optional badge (count). Content is a `ContentControl` bound to the selected page's ViewModel, resolved to views with implicit `DataTemplate`s registered by each feature. Each page ViewModel implements `IPage` (`Title`, `Glyph`, `Badge` (string?, observable), `Order`, and `OnNavigatedToAsync(CancellationToken)` called on first and each subsequent navigation).
6. **Placeholder pages** for all four features: a ViewModel implementing `IPage` and a View saying "Coming soon" - so later milestones only replace files inside their own feature folder.
7. **Admin support**: `IElevationService` (Core) with `IsElevated` and `RestartElevated()` (relaunch `Environment.ProcessPath` with `runas`, then shut down; user cancelling UAC is not an error). Sidebar footer shows "Running as administrator" or a "Restart as admin" button. A reusable `AdminRequiredBanner` user control for feature pages.
8. **CI**: `.github/workflows/ci.yml` on push to `main` and all PRs: `windows-latest`, `actions/setup-dotnet` using `global.json`, restore, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`. Cache NuGet. Also `.github/dependabot.yml` for `nuget` and `github-actions`, weekly.
9. **Repo hygiene**: `.github/pull_request_template.md` (summary, testing, screenshots, acceptance checklist), `CONTRIBUTING.md` (branching, commit style, how to build/run/test), `CHANGELOG.md` (Keep a Changelog format, `Unreleased` section), `LICENSE` (MIT, copyright "El1rans"), README updated with build instructions.

## Acceptance criteria

- [ ] `dotnet build -c Release` from repo root: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release` runs and passes (at least tests for `SettingsStore`: round-trip, missing file, corrupt file backup).
- [ ] App starts, follows Windows light/dark theme, shows 4 nav items; clicking each switches the page; no binding errors in debug output.
- [ ] Sidebar shows correct admin state; "Restart as admin" relaunches elevated.
- [ ] Log file is created under `%APPDATA%\Porchlight\logs`.
- [ ] CI workflow passes on the PR.
