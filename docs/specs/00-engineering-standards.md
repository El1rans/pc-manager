# 00 - Engineering standards

> **Renamed to Porchlight.** This product was originally built and specced as "PC Manager"; milestone 08 (`docs/specs/08-rebrand-porchlight.md`) renamed it to Porchlight. Specs 01-07 below were written before the rename and still describe the app as "PC Manager" in places - read `PCManager`/`PC Manager` there as `Porchlight`/the current product name, except where a spec is specifically documenting pre-rebrand or legacy-migration behavior (e.g. the installer's legacy registry key, the app's legacy mutex name).

Every milestone spec in this folder builds on this document. If a milestone spec and this file disagree, the milestone spec wins for that milestone only.

## Product

Porchlight is a Windows 10/11 desktop app for monitoring and maintaining a single PC: live stats, app updates (winget), hardware sensors and fan control, and RGB lighting.

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (`net10.0-windows`), SDK pinned in `global.json` (`10.0.400`, `rollForward: latestFeature`) |
| UI | WPF with the built-in Fluent theme (`Application.ThemeMode = ThemeMode.System`, follows Windows light/dark) |
| MVVM | CommunityToolkit.Mvvm 8.4.x (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) |
| Composition | Microsoft.Extensions.Hosting generic host + DI; `App` builds the host and resolves `MainWindow` |
| Logging | Microsoft.Extensions.Logging with Serilog file sink at `%APPDATA%\Porchlight\logs\porchlight-.log` (daily roll, 14 files kept) |
| Settings | JSON at `%APPDATA%\Porchlight\settings.json` via the singleton `ISettingsStore`; one shared `Current` instance. Features change settings ONLY through `Update(s => ...)` (mutates + saves under a lock); never mutate `Current` directly |
| Tests | xUnit v3 on Microsoft.Testing.Platform (`dotnet test` via the `test.runner` setting in `global.json`); no UI automation tests |
| Packages | Central Package Management (`Directory.Packages.props`), exact versions, no floating versions, no prerelease unless a spec says so |

## Solution layout

```
Porchlight.slnx
global.json
Directory.Build.props        # shared compiler/analyzer settings
Directory.Packages.props     # central package versions
.editorconfig
src/
  Porchlight.Core/            # net10.0-windows class library. NO WPF references.
    <Feature>/               # e.g. Winget/, Monitoring/, Hardware/, Lighting/
  Porchlight.App/             # WPF exe. Views, ViewModels, controls, DI wiring.
    Features/<Feature>/      # <Feature>View.xaml, <Feature>ViewModel.cs, feature-only controls
    Shell/                   # MainWindow, navigation
    Controls/, Themes/
tests/
  Porchlight.Core.Tests/
prototype/                   # original PowerShell prototype, kept for reference, not built
docs/specs/
```

Rules:
- All logic that can be tested without a window lives in `Porchlight.Core` behind an interface. ViewModels depend on interfaces, never on `Process`, WMI, registry, or hardware libraries directly.
- One public type per file; file name = type name. Namespaces follow folders (`Porchlight.Core.Winget`).
- A feature adds itself to the app by: its own folder in Core and App, and one DI registration extension method (`services.Add<Feature>Feature()`) that calls `services.AddPage<TViewModel, TView>()` and registers its services. It does not edit other features' files.
- Third-party tools a feature needs (AnyDesk, OpenRGB, PawnIO) are detected, installed and started only through `IComponentService` (milestone 01b), and a missing tool is shown with the shared `ComponentCard` control.
- Processes (winget, AnyDesk CLI, etc.) are started only through the shared `IProcessRunner` from 01b.

## Code quality gates (enforced by `Directory.Build.props`)

- `Nullable` enabled, `TreatWarningsAsErrors` true, `AnalysisLevel` `latest-recommended`, `EnforceCodeStyleInBuild` true, `LangVersion` latest.
- No `async void` except event handlers. Every async call that can be cancelled takes a `CancellationToken`.
- Never block the UI thread: process launches, WMI, performance counters, hardware reads and network calls run off the dispatcher. UI updates are marshalled back (e.g. `IProgress<T>` created on the UI thread, or `Dispatcher`).
- No swallowed exceptions without logging. A `catch` that ignores an error must log at Debug or higher and say why ignoring is safe.
- `IDisposable` resources (counters, processes, hardware handles, sockets) are disposed; long-lived ones are owned by a DI singleton that disposes on shutdown.
- No magic numbers for thresholds or intervals; use named constants or settings.
- User-visible text is plain, short, and sentence case. Errors tell the user what to do next.

## UI conventions

- Use Fluent theme brushes via `DynamicResource` (for example `TextFillColorSecondaryBrush`, `CardBackgroundFillColorDefaultBrush`, `AccentFillColorDefaultBrush`, `SystemFillColorCautionBrush`, `SystemFillColorCriticalBrush`, `SystemFillColorSuccessBrush`). No hard-coded colors except inside a chart's own palette.
- Shared styles live in `Themes/Styles.xaml` (`Card`, `CardTitle`, `PageTitle`, `Secondary`, `Icon`).
- Icons: `Segoe Fluent Icons` with `Segoe MDL2 Assets` fallback.
- Status is never color alone: pair status colors with an icon and a text label.
- Charts: one metric per chart, one accent hue, thin 2px lines, recessive baseline, no dual axes. Single-series charts need no legend; the tile title names the metric.
- Every page works from 900x600 up. Long lists scroll; nothing overflows horizontally.
- Pages whose feature needs admin rights show a clear banner with a "Restart as administrator" button instead of failing silently.

## Git workflow

- `main` is always releasable. Nobody commits to `main` directly; every change lands through a pull request.
- Branch names: `feat/<short-name>`, `fix/<short-name>`, `chore/<short-name>`, `docs/<short-name>`.
- Commits follow Conventional Commits (`feat(updates): ...`, `fix(dashboard): ...`, `test(core): ...`, `chore(ci): ...`). Small, focused commits; each one builds.
- Before opening or updating a PR: `git fetch origin && git rebase origin/main`, then `dotnet build -c Release` and `dotnet test -c Release` must pass locally.
- PR description: what changed, how it was tested, screenshots for UI changes when possible, and a checklist of the spec's acceptance criteria.
- PRs are squash-merged after review; the branch is deleted after merge.
- Every commit message ends with the co-author trailer line: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` (dev agents) or `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (Opus).
- PR descriptions end with: `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Definition of done (every milestone)

1. All acceptance criteria in the milestone spec are met.
2. `dotnet build -c Release` has zero warnings and zero errors.
3. `dotnet test -c Release` passes; new Core logic has unit tests (parsers, calculations, state machines, safety rules).
4. The app launches and the new page renders without binding errors (check the debug output for `System.Windows.Data Error`).
5. README "Features" section updated if user-visible behaviour changed.
6. PR opened against `main` with CI green.
