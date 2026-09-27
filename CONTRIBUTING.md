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
  real OpenRGB SDK server (`src/Porchlight.Core/Lighting/Demo/DemoLightingService.cs`,
  `LightingServiceCollectionExtensions.AddLightingCore`).

The check and every fake above only exist in a DEBUG build (`#if DEBUG`); a Release build - what CI
compiles and what a release ships - is unaffected and does not contain this code at all. Unset the
environment variable (or just close the terminal) to go back to showing your real data next time
you run Porchlight for actual use.

If a page you're screenshotting shows something demo data doesn't cover yet (e.g. a new feature's
own machine-identifying data), extend `AddMonitoring`'s demo branch and the relevant `Demo.*`
fake, rather than redacting a real screenshot after the fact.

## Code organization

See `docs/specs/00-engineering-standards.md` for the full solution layout and code quality gates.
In short: testable logic lives in `Porchlight.Core` behind an interface; `Porchlight.App` holds
views, view models, and DI wiring. A feature adds itself via its own folders, one DI registration
extension method, and one navigation entry, without touching other features' files.
