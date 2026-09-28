# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- Renamed to Porchlight (from "PC Manager"), with a new brand kit (icon, logo, installer wizard
  images - see `assets/brand/`). Code, projects, assemblies, the solution file, the installer
  script and CI/release paths all renamed from `PCManager.*`/`PCManager-Setup-*` to
  `Porchlight.*`/`Porchlight-Setup-*`. An existing install upgrades in place (same installer
  `AppId`) to `Program Files\Porchlight`; settings and the fan-control activity marker are
  migrated automatically on first run from `%APPDATA%\PCManager` to `%APPDATA%\Porchlight` (the
  old folder is left untouched - see `AppDataMigrator`). The installer's `AppMutex` and the
  first-run setup's installer-handled-components registry read both still recognize the
  pre-rebrand names/keys so an in-place upgrade behaves correctly. See
  `docs/specs/08-rebrand-porchlight.md`.

### Added

- Screenshots: a DEBUG-only "demo data" mode (`PORCHLIGHT_DEMO_DATA=1`, see CONTRIBUTING.md;
  renamed from `PCMANAGER_DEMO_DATA` as part of the Porchlight rename above) that swaps the
  Dashboard's system-info, drive and process-list services for fake ones - and, as of the rename,
  also `IWingetClient` (Updates), `IHardwareService` (Hardware), `ILightingService` (Lighting) and
  `IAnyDeskService` (Get help) - so documentation screenshots no longer need to show a real PC's
  computer name, hardware, drive labels, installed apps, sensors, RGB devices or AnyDesk address.
  Does not exist in a Release build. Replaced every screenshot in
  `docs/screenshots/` that showed real machine-identifying data with a redacted version, cropped
  `first-run.png` down to just the app window (it previously captured the whole desktop), and
  removed two obsolete/unused screenshots (`after-skip.png`, `foundation.png`).
- Code signing: applied for free code signing through the SignPath Foundation program for open
  source projects. `docs/CODE_SIGNING_POLICY.md` documents the required policy (team roles,
  privacy statement); `docs/signing/` holds the SignPath artifact configurations (each requiring a
  `version` metadata parameter/product-version restriction) and a draft application.
  `.github/workflows/release.yml` now builds and signs in one job (`build-sign`, read-only
  permissions) and publishes the GitHub Release in a separate job (`publish`, the only one with
  `contents: write`) that only downloads what `build-sign` produced; it submits the published
  `PCManager.exe` and the compiled installer to SignPath for signing on every tagged release,
  verifying the resulting Authenticode signature (including its timestamp), and falls back to
  publishing an unsigned release with a visible warning both in the workflow log and in the release
  notes themselves until the SignPath project is approved and configured (see `docs/RELEASING.md`;
  an optional `SIGNPATH_REQUIRED` repository variable instead fails the release outright when
  signing isn't configured). Added `SECURITY.md` describing how to report vulnerabilities privately
  via GitHub.
- Updates: a "Check for updates when PC Manager starts" toggle on the Updates page (on by default,
  matching prior behaviour) that controls whether `UpdatesAutoCheckHostedService` runs a `winget
  upgrade` listing in the background on every app start - see `docs/CODE_SIGNING_POLICY.md`'s
  privacy statement, which now discloses this background `winget` call and links Microsoft's,
  AnyDesk's and OpenRGB's own privacy documentation for the network activity each is responsible
  for.
- Installer and releases: a self-contained, single-file win-x64 publish profile; an Inno Setup 6
  script (`installer/PCManager.iss`) that installs PC Manager per-machine to
  `Program Files\PC Manager` with Start menu/desktop/startup shortcuts, an optional Components
  page (AnyDesk ticked by default; OpenRGB and the PawnIO driver unticked) that installs each
  choice via `winget` and writes the `HKLM\Software\PC Manager\Installer\Components` marker
  first-run setup reads; and an uninstaller that leaves AnyDesk/OpenRGB/PawnIO in place and asks
  before deleting the current user's `%APPDATA%\PCManager` data. A generated app icon. A single
  `Version` in `Directory.Build.props`, shown in the sidebar footer. CI now compiles the installer
  and uploads it (with a SHA-256 checksum) as a build artifact on every PR/push to `main`; a new
  `v*.*.*` tag builds, tests, compiles the installer and publishes a GitHub Release with the setup
  exe, its checksum, and the matching CHANGELOG section as release notes.
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

- Closing the app logged "Error while shutting down the host" (an `ObjectDisposedException`) on
  every exit. Every page view model is registered with the DI container twice (as itself and,
  through a forwarding factory, as `IPage`), so the container disposes it twice on shutdown; the
  "Get help" page's second `Dispose` then disposed its AnyDesk `ComponentCardViewModel` again, which
  cancelled an already-disposed `CancellationTokenSource`. `Dispose` on `ComponentCardViewModel`,
  every page view model (Dashboard, Updates, Hardware, Lighting, Get help) and `MainViewModel` is
  now idempotent, and the Hardware page now disposes the PawnIO card it creates (it previously never
  did). Shutdown also no longer stops at the first failing page, so later pages are disposed too.

- The main window (and the first-run "Choose what to set up" dialog) could paint its content area
  solid white for a moment after appearing, until the user clicked it - a known WPF Fluent-theme
  rendering gap: Desktop Window Manager (DWM) starts compositing a window's surface as soon as it
  is shown, but WPF's render thread does not hand it a painted frame until some milliseconds later,
  and until then the surface is unpainted (white). Normally too brief to notice, but on a slower
  machine (or under load) it can persist well past that, which is alarming for Porchlight's
  non-technical target users. Reproduced this directly: on this dev machine, throttling the process
  to one CPU core at low priority (no app change) reliably exposed a solid-white first frame on
  screen (10/10 repro runs), while the same test never showed one after the fix (0/10). The new
  `WhiteFlashGuard` (`Porchlight.App/Shell/WhiteFlashGuard.cs`) cloaks the window with
  `DWMWA_CLOAK` from the moment its native handle exists until WPF has actually presented a
  rendered frame, so DWM never gets a chance to composite the blank surface in the first place -
  see https://github.com/scjv/wpf-window-white-flash for the underlying investigation this fix is
  based on. Cloaking alone would trade one failure mode for a worse one - a window that never
  reaches `ContentRendered` (an exception during its first layout/render pass, or being shown
  minimized) would otherwise stay cloaked, i.e. invisible but still present in the taskbar, forever.
  `CloakLifecycle` (`Porchlight.App/Shell/CloakLifecycle.cs`, unit tested) guarantees an uncloak via
  a bounded fallback timer regardless of what happens on the render path; `WhiteFlashGuard` also
  forces an uncloak on the window's state changing (e.g. restored from minimized) or closing, and
  exposes `UncloakAll()`, called from the app's unhandled-exception handler so a crash during a
  window's first render can never leave its owned error dialog hidden behind it. Manually verified
  the fallback path by forcing `ContentRendered` to never fire: the window stayed fully invisible
  until the fallback timer elapsed, then appeared already fully painted.
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
- Fixed two more intermittent CI test failures on `windows-latest`, both tied to real wall-clock
  timing rather than the safety logic itself:
  - `FanControlManager`'s watchdog (armed/owning-but-stalled detection) now reads "now" from an
    injected `TimeProvider` (defaulting to `TimeProvider.System`) and creates its polling timer via
    `TimeProvider.CreateTimer` instead of a raw `System.Threading.Timer`. Tests drive a
    `FakeTimeProvider` and advance it explicitly, so `Watchdog_ActiveAndOwningButNoSnapshotForTooLong_PausesAndRestoresOwnedFans`
    and `ResumeFromSuspend_AfterLongPause_DoesNotImmediatelyTripWatchdog` no longer depend on a real
    `Thread.Sleep` racing a real timer thread. No change to watchdog thresholds or gating.
  - `SettingsStore`'s bounded retry backoff (10/25/50/100/200 ms) around the settings-file move is
    now driven through an injectable sleeper (production still uses `Thread.Sleep`). The
    transient-lock test now uses a fake sleeper that releases the lock deterministically on the
    first retry instead of racing a real 50 ms `Timer` against the retry budget, which could
    occasionally fire late under CI load and exhaust the retries before the lock was released.
