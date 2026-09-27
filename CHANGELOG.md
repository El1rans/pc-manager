# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Foundation: solution layout, host + DI, Serilog file logging, JSON settings store with atomic
  writes and corrupt-file recovery, WPF shell with navigation rail (Dashboard, Updates, Hardware,
  Lighting placeholders), admin elevation support, CI workflow and repo hygiene files.
- Dashboard: a live overview of the PC, sampled once a second off the UI thread and paused while
  the window is minimized. Metric tiles for CPU, memory, GPU, disk and network throughput with
  60-second sparklines and min/avg/max tooltips; a drives card with a low-space warning; a top
  processes card; a restart-pending badge; and a system info card (computer name, OS, CPU, GPU,
  RAM). Measured self-CPU stays under 1% of total system CPU on an 8-core/16-thread machine.
- Updates: `IWingetClient`/`WingetTableParser`/`WingetExitCodes` in `PCManager.Core.Winget`/
  `PCManager.Core.Processes`, and the Updates page - lists `winget upgrade` results with default
  selection, filter, ignore list (persisted), Silent/Include-unknown options, one-at-a-time update
  run with live status/log, "Stop after current", and a nav badge showing the non-ignored update
  count. Checks automatically at startup and on Refresh.
- Remote support: a "Get help" page, pinned in its own group at the bottom of the nav rail, that
  installs and starts AnyDesk through the shared component service, reads its ID/alias
  (`AnyDesk.exe --get-id`/`--get-alias`, falling back to `system.conf`/`service.conf` if that
  fails), and shows it large and grouped in threes with a digits-only "Copy address" button, a
  start/status control, plain instructions, a scam-safety note, and a "Copy support info" share
  action. An optional helper name (edited through a small confirm dialog) is remembered in
  settings and shown read-only on the page.
- Hardware: sensors tab (filterable tree grouped by hardware, current/min/max, reset min/max) and
  fans tab (per-fan mode - Default/Fixed/Curve - with a draggable-point curve editor), backed by
  LibreHardwareMonitorLib 0.9.6 on a dedicated background thread. Software fan control is off by
  default, requires a one-time risk confirmation, is only ever active while the page reports the
  driver/elevation are actually `Ready`, and is enforced by a fully unit-tested safety engine: a
  minimum speed floor, an overheat failsafe (100% until 10 C below the threshold), a lost/stale
  sensor failsafe, a no-CPU-temperature failsafe, and a set-failure failsafe that restores every fan
  to BIOS control and disables software control. Every fan is restored to BIOS control on exit,
  system suspend, session end, and an unhandled exception - except after a forced kill, crash, BSOD
  or power loss, which only a PC restart (not relaunching PC Manager) can recover from; a banner
  warns if that was left behind by the previous session.
- Lighting: RGB control via OpenRGB - device list with per-device color and mode, "all devices"
  color/brightness card with up to 8 saved favorite colors, OpenRGB profile loading, and the "Start
  OpenRGB with PC Manager" toggle. `ILightingService` times out and reconnects cleanly if OpenRGB is
  closed or unreachable, including a silent remote close (detected via a 5 second heartbeat, since
  the underlying socket never surfaces one as an error - see `docs/upstream/openrgb-net.md`), with
  unit and real-loopback-socket tests covering `RgbColor`, mode selection, and disconnect/timeout
  handling. Talks to OpenRGB through a vendored, patched copy of `OpenRGB.NET` (see
  `THIRD-PARTY-NOTICES.md`) rather than the unpatched NuGet package.

### Changed

- First-run setup: only AnyDesk is pre-ticked by default on the "Choose what to set up" screen
  (when not already installed/handled by the installer). OpenRGB and the PawnIO fan driver always
  start unticked - installing a background app and a kernel driver should be an explicit opt-in,
  not a default.

### Fixed

- `SettingsStore.Save`/`Update` no longer throw when the settings file is transiently locked by
  another process (e.g. Defender, the Search indexer, or OneDrive briefly holding the file during
  the atomic rename). The final move now retries with bounded backoff, and if it still fails,
  persistence is treated as best-effort: a warning is logged, in-memory `Current` stays
  authoritative, and the next save retries. This fixes intermittent `UnauthorizedAccessException`
  crashes on startup (`PC Manager could not start`) caused by the launch-count update.
- `SetupViewModel.Dispose` is idempotent - the setup window's own Closed handler and the DI
  container (which also disposes transients it created) could both dispose it, and the second
  `Cancel()` on an already-disposed `CancellationTokenSource` was logged as an ERR on every app exit
  after first-run setup had been opened.
- Fixed a flaky "Get help" test (seen once on CI, passing on rerun): `RemoteSupportViewModel`'s
  address poll loop and copy-confirmation reset ran off a real `PeriodicTimer`/`Task.Delay` tied to
  the wall clock, so a real timer tick could occasionally be delayed past the test's own time
  budget under CI load. A `TimeProvider` is now injected (defaulting to `TimeProvider.System`) and
  tests drive a `FakeTimeProvider` explicitly instead of depending on real timer scheduling.
- Hardware/fan control: after `IFanController.RestoreDefault()` hands an owned fan back to BIOS
  control, `FanControlManager` now verifies on the following ticks that the channel actually left
  software mode (`IFanController.IsUnderSoftwareControl`, from LHM's `IControl.ControlMode`) -
  some SuperIO/NVAPI backends can accept the call without error yet leave it in software mode.
  A fan still stuck after 3 ticks gets up to 3 `RestoreDefault` retries (never `SetPercent`); if it
  still never clears, a critical banner tells the user to restart their PC to return it to BIOS
  control. Closes a known gap left open by the hardware/fan-control milestone (PR #11).
