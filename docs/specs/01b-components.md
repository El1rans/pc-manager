# 01b - Components and first-run setup (branch `feat/components`)

Runs after 01-foundation and BEFORE the feature milestones 02-06, which depend on it.

Goal: one shared way to detect, install and start the third-party tools that features need, so every feature page and the first-run setup behave the same.

## Components

| Component id | Needed by | winget id | Detect | Notes |
|---|---|---|---|---|
| `anydesk` | Get help (06) | `AnyDesk.AnyDesk` | exe in Program Files (x86)/Program Files or uninstall registry | |
| `openrgb` | Lighting (05) | `OpenRGB.OpenRGB` | exe via uninstall registry / Program Files / winget install location; also accept a user-set path | Needs to run with `--server` for the SDK. Portable installs allowed via path setting. |
| `pawnio` | Hardware sensors + fan control (04) | `namazso.PawnIO` | installed per uninstall registry and its driver service present (verify the service name from PawnIO docs) | Kernel driver; install needs admin (UAC). Verify LibreHardwareMonitorLib version in use actually uses PawnIO. |

Verify each winget id with `winget show --id <id> --exact` during development.

## Core (`PCManager.Core/Components`)

- `IProcessRunner` (move the generic winget process-running and output-splitting code here if the Updates milestone would otherwise own it: `WingetOutputReader`, running `winget.exe` with `ArgumentList`, UTF-8, streaming lines/progress). `IWingetClient` (02) builds on it.
- `ComponentDefinition` (id, display name, one-line purpose in plain language, winget id, requires admin, detect strategy, optional post-install action).
- `IComponentService`:
  - `GetStatusAsync(id, ct)` -> `ComponentStatus(State: NotInstalled | Installed | Running | Error, Version?, Path?, Message?)`.
  - `InstallAsync(id, IProgress<string> log, IProgress<string> progress, ct)` via `winget install --id <id> --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity`. Exit code `0x8A15002B`/"already installed" counts as success. Re-detects after install.
  - `StartAsync(id, ct)` for apps that need to be running (OpenRGB: `--server --startminimized`; AnyDesk: normal start).
  - `StatusChanged` event so pages update when a component gets installed from elsewhere (setup page, another page).
- Detection uses an `IRegistryReader` + `IFileSystem` abstraction so it is unit-testable.

## App

- **Reusable `ComponentCard` control**: icon, name, purpose text, state (icon + text), and one primary action matching the state: "Install" -> progress bar + collapsible log -> "Start" (if needed) -> "Ready". Shown by feature pages (04, 05, 06) when their component is missing. Admin-required components show "Needs administrator approval" and trigger a UAC prompt through winget; if the user declines, show "Installation was cancelled" with Retry.
- **First-run setup**: on first launch (and from Settings later via "Set up optional features"), a page/dialog: "Choose what to set up" with a checkbox per component (name + plain purpose: "Remote help from family (AnyDesk)", "RGB lighting control (OpenRGB)", "Fan control and temperature sensors (PawnIO driver)"). Components already installed show "Already installed" and are not ticked. **Default tick state**: only AnyDesk is pre-ticked, and only when it is not already installed and the installer marker (see "Contract with first-run setup" below) does not list it as already handled - it is the one component a parent needs for remote help to work at all. OpenRGB and the PawnIO fan driver always start unticked by default (cosmetic / kernel-driver installs respectively), regardless of install state or the installer marker; the user opts in explicitly. "Set up" installs ticked ones one after another with per-item status; "Skip" closes. Remember that first-run was shown (`Setup.FirstRunCompleted`).
- **OpenRGB autostart option** (Lighting settings): "Start OpenRGB with PC Manager" - when on, PC Manager starts OpenRGB minimized with the SDK server if it is not running.

## Tests

Detection logic with fake registry/file system; install flow with a fake process runner (success, already installed, failure, cancelled UAC -> distinct exit code handling); first-run state persistence.

## Acceptance criteria

- [ ] First launch shows the setup choices; installing each component works; skipping works and it is not shown again.
- [ ] A feature page with a missing component shows the `ComponentCard` and installs in place, then the page switches to its normal content without restarting the app.
- [ ] Declining UAC for PawnIO gives a clear message and Retry.
