# Contributing

## Branching

- `main` is always releasable. Nobody commits to `main` directly; every change lands through a
  pull request.
- Branch names: `feat/<short-name>`, `fix/<short-name>`, `chore/<short-name>`, `docs/<short-name>`.

## Commit style

Commits follow [Conventional Commits](https://www.conventionalcommits.org/):
`feat(updates): ...`, `fix(dashboard): ...`, `test(core): ...`, `chore(ci): ...`. Keep commits
small and focused; each one should build.

## Building, running, and testing

Prerequisites: .NET SDK matching `global.json` (Windows, since the app uses WPF).

```powershell
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project src/Porchlight.App
```

Before opening or updating a pull request:

```powershell
git fetch origin
git rebase origin/main
dotnet build -c Release
dotnet test -c Release
```

Both must pass with zero warnings and zero errors before requesting review.

`dotnet test` runs on Microsoft.Testing.Platform (see `global.json`), which rejects old VSTest-era
switches: `dotnet test --nologo`, for example, makes every test app exit with "Zero tests ran".

### Code coverage

```powershell
dotnet test -c Release --coverage --coverage-output-format cobertura --coverage-settings coverage.config
```

This writes one `TestResults/<guid>.cobertura.xml` per test project. `coverage.config` limits the
report to Porchlight's own assemblies and leaves out source-generated code. CI runs the same
command and uploads the reports as the `coverage` artifact. Code that only wraps a Windows API
(WMI, the registry, P/Invoke, tray/window plumbing) is not expected to be covered by unit tests;
parsers, calculations, state machines, safety rules and view models are.

## Pull requests

- Use the PR template: summary, how it was tested, screenshots for UI changes, and a checklist of
  the spec's acceptance criteria.
- PRs are squash-merged after review; the branch is deleted after merge.
- CI (`.github/workflows/ci.yml`) must be green before merge.

## Screenshots

`docs/screenshots/` holds the screenshots used in documentation. **Never commit a screenshot taken
against your real PC** - it will show your real computer name, drive labels, installed apps and
hardware. Instead, capture screenshots using the DEBUG-only demo data mode:

```powershell
$env:PORCHLIGHT_DEMO_DATA = "1"
dotnet run -c Debug --project src/Porchlight.App
```

This swaps out every service that would otherwise show real machine data for a fake one reporting
made-up data - everything else in the app runs normally:

- Dashboard: system-info, drive and process-list services (`src/Porchlight.Core/Monitoring/Demo/`,
  `MonitoringServiceCollectionExtensions.AddMonitoring`).
- Get help: `IAnyDeskService`, reporting a fixed, obviously-not-real address
  (`src/Porchlight.Core/RemoteSupport/Demo/DemoAnyDeskService.cs`,
  `RemoteSupportServiceCollectionExtensions.AddRemoteSupportCore`).
- Updates: `IWingetClient`, reporting a fixed, made-up list of "available upgrades" instead of your
  real installed apps (`src/Porchlight.Core/Winget/Demo/DemoWingetClient.cs`,
  `WingetServiceCollectionExtensions.AddWingetClient`).
- Hardware: `IHardwareService`, reporting a fixed, made-up sensor tree and fake fans that don't
  control anything real (`src/Porchlight.Core/Hardware/Demo/DemoHardwareService.cs`,
  `HardwareServiceCollectionExtensions.AddHardwareCore`).
- Lighting: `ILightingService`, reporting a fixed, made-up device list instead of connecting to a
  real OpenRGB SDK server, and `ILightingConflictDetector`, reporting no conflicting lighting software
  (`src/Porchlight.Core/Lighting/Demo/`, `LightingServiceCollectionExtensions.AddLightingCore`).
- Update history: a made-up week of history (`src/Porchlight.Core/Winget/Demo/FakeUpdateHistoryStore.cs`).
- Startup apps, Running apps and Services: made-up lists that never read or change the real startup
  items, processes or services (`src/Porchlight.Core/Startup/Demo/`, `src/Porchlight.Core/RunningApps/Demo/`,
  `src/Porchlight.Core/WindowsServices/Demo/`).

The check and every fake above only exist in a DEBUG build (`#if DEBUG`); a Release build - what CI
compiles and what a release ships - is unaffected and does not contain this code at all. Unset the
environment variable (or just close the terminal) to go back to showing your real data next time
you run Porchlight for actual use.

### Isolated data folder

Demo and test runs never touch your real `%APPDATA%\Porchlight` (settings.json, logs, custom
animations, fan-control marker). Every per-user path comes from one place,
`src/Porchlight.Core/Settings/AppDataPaths.cs`, and in a DEBUG build it is redirected:

- `PORCHLIGHT_DEMO_DATA=1` on its own uses `%TEMP%\Porchlight-demo`. On first use it is seeded
  with a `settings.json` that marks first-run as completed, so the setup wizard doesn't appear.
  Delete the folder to start fresh.
- `PORCHLIGHT_DATA_DIR=<absolute path>` uses that folder instead (with or without demo data) - e.g.
  a throwaway folder per visual check, or to test the first-run wizard on an empty folder. A
  relative path is rejected at startup rather than falling back to the real folder.

```powershell
$env:PORCHLIGHT_DATA_DIR = "$env:TEMP\porchlight-check"
dotnet run -c Debug --project src/Porchlight.App
```

A run with either override also uses its own single-instance lock, so it starts alongside a real,
running Porchlight instead of just bringing that one to the front.

While either override is active, the one-time `PCManager` -> `Porchlight` folder migration is
skipped entirely. A Release build ignores both variables. There is no need to back up or restore
your real `settings.json` before a demo/test run. (Only the self-update installer download cache,
`%LOCALAPPDATA%\Porchlight\Updates`, is not redirected - it holds no settings.)

If a page you're screenshotting shows something demo data doesn't cover yet (e.g. a new feature's
own machine-identifying data), extend `AddMonitoring`'s demo branch and the relevant `Demo.*`
fake, rather than redacting a real screenshot after the fact.

## Code organization

See `docs/specs/00-engineering-standards.md` for the full solution layout and code quality gates.
In short: testable logic lives in `Porchlight.Core` behind an interface; `Porchlight.App` holds
views, view models, and DI wiring. A feature adds itself via its own folders, one DI registration
extension method, and one navigation entry, without touching other features' files.

## The prototype

`prototype/` holds the original PowerShell winget updater tool that the Updates page was ported from. It is kept for reference and is not built. To run it standalone, double-click `prototype/Winget Updater.bat`.
