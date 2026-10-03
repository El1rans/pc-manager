# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- New **Safety** page under Internet & safety, a calm answer to "is this PC safe?": whether the
  antivirus and Windows Firewall are on and up to date (Windows Firewall is read directly), when
  Windows last installed updates and whether they keep failing (with a "Check now" for updates
  waiting), and which remote-control programs (TeamViewer, AnyDesk, RustDesk and others) are
  installed or running. Porchlight's own AnyDesk is labelled as set up by Porchlight
  (`docs/specs/31-safety-status.md`).
- **Browser add-ons** now also checks each browser's home page, start-up pages, new-tab page and
  search engine, and says "Looks fine", "Changed" or "Forced by a setting on this PC". Read-only: it
  only points you to the browser's own settings (`docs/specs/32-browser-hijack-check.md`).
- **Backup** card on the Health check page: is File History or OneDrive backing up your files, and
  when did it last run? It also appears as a "Backups" section in the check-up report
  (`docs/specs/33-backup-status.md`).
- **Recent changes** page under Tune-up: a list of what Porchlight changed (startup items, services,
  cleanups, installs, updates, removed apps), newest first, with Undo where it is possible. Settings > General
  has a new "Create a restore point before big changes" option (on by default) that asks Windows for a
  restore point before a batch of app updates or a service start-type change. The Services page can
  now set "Starts with Windows (delayed)" (`docs/specs/34-recent-changes.md`).
- **Remove apps** page under Apps & services: installed programs in a plain list with search and
  sorting, a careful Remove button (winget first, then the program's own uninstaller), "Often
  preinstalled" hints and system parts kept in a separate group
  (`docs/specs/35-remove-apps.md`).
- **Printers** page under Hardware, for "my printer won't print": each printer's state in plain
  words, make default, clear stuck print jobs, print a test page, use an offline printer online, a
  guided "Fix my printer" and restart the print service (`docs/specs/36-printer-fixes.md`).
- **Text size** setting (Normal, Large, Extra large) in Settings > General that makes the text and
  pictures on each page bigger straight away (`docs/specs/37-larger-text.md`).
- Optional **check-up reminder** in Settings > Notifications: a tray reminder every week, 2 weeks or
  month to send the check-up report to your helper. The Get help page shows when the last check-up
  was and when the next reminder is. Porchlight still never sends anything itself
  (`docs/specs/38-checkup-reminder.md`).
- The **web console** has three new read-only views: Security, Updates waiting and Startup impact,
  with a small menu at the top to jump between them (`docs/specs/39-web-console-more.md`).

### Changed

- Removing a program from the Remove apps page is recorded in Recent changes (as "Can't be undone").

### Fixed

- A flaky test of the Updates log (a race when entries were written close together) no longer fails
  builds at random.

## [0.2.1] - 2026-10-03

### Fixed

- Porchlight 0.2.0 failed to start ("Porchlight could not start") because two services the Startup
  apps page needs were only registered in the developer demo mode. A new test now builds the app's
  real set of services on every CI run, so this kind of mistake fails the build instead of the app.
  If 0.2.0 won't start for you, download and run the 0.2.1 installer from the Releases page - the
  in-app updater can't run while the app doesn't start.

## [0.2.0] - 2026-10-03

### Added

- New **Apps & services** section in the left menu with three pages:
  - **Get apps** - search the official `winget` catalog and install a program in one click, with a
    list of popular apps (`docs/specs/27-get-apps.md`).
  - **Running apps** - what's running right now with CPU and memory use, grouped like Task Manager,
    with a safe "End task" and "Open file location" (`docs/specs/28-running-apps.md`).
  - **Services** - background services that other programs installed, explained in plain words, with
    start/stop and start-type changes for non-Windows services (`docs/specs/29-windows-services.md`).
- **Update history** on the Updates page: every update, reinstall and install run from Porchlight,
  grouped by day (`docs/specs/26-update-history.md`).
- **Startup apps** shows a High/Medium/Low startup impact for each item from Windows' own
  measurements, can sort by it, and also lists scheduled tasks that run at sign-in, which can be
  turned off and on (`docs/specs/30-startup-impact-and-tasks.md`).

### Changed (for developers)

- CI is faster: the installer build runs alongside build-and-test instead of after it, a new push to a
  pull request cancels its older run, and docs-only pull requests skip the Windows build.
- DEBUG demo runs (`PORCHLIGHT_DEMO_DATA=1`) can start while a real Porchlight is running, and no
  longer show which lighting software is really running on the PC.

## [0.1.1] - 2026-10-02

### Added

- Theme setting: choose "Match Windows" (default), "Light" or "Dark" in the new "Appearance" card of the
  Notifications dialog; the choice is saved and applied to all open windows immediately
  (`docs/specs/25-theme-setting.md`). The sidebar and tab badge text now uses the theme's own
  on-accent colour instead of fixed white.
- Self-update: an installed Porchlight now checks GitHub Releases for a newer version (at startup, if
  "Check for updates when Porchlight starts" is on, and whenever you press Refresh on the Updates
  page) and shows "Porchlight X.Y.Z is available" at the top of that page, with a "What's new" link
  and an "Update now" button. Update now downloads the installer, checks its SHA-256 against the
  release's checksum file, starts it (one Windows permission prompt), and Porchlight restarts itself on
  the new version. A copy that wasn't installed with the installer only gets a link to the release
  page. Downloads are accepted only from GitHub over https. Design and privacy details:
  `docs/specs/23-self-update.md`, `docs/CODE_SIGNING_POLICY.md`.
- "Start Porchlight when I sign in to Windows" option in the Notifications dialog
  (`docs/specs/24-start-at-login.md`). It registers a Task Scheduler task that starts Porchlight in the
  tray as administrator with no UAC prompt at sign-in, so every sensor works from the first minute;
  turning it on or off asks for admin permission once. The checkbox shows the task's real state, the
  task is re-pointed if Porchlight moves, and uninstalling removes it. New `--tray` switch starts
  Porchlight hidden in the tray.
- The main window remembers its size, position and maximized state between launches (falling back
  to the default if the saved spot is no longer on any screen).

### Changed

- The sidebar footer buttons "Set up optional features" and "Notifications" are replaced by a **Settings** page
  pinned at the bottom of the left menu, with General (theme, start at sign-in, keep in tray), Notifications
  (alerts, update checks) and Optional features (AnyDesk, OpenRGB, PawnIO) tabs. The tray's "Notifications
  settings" opens it. The left menu's selected item is lighter in Light theme.
- Dashboard: CPU shows on the very first update instead of waiting ~1 s (5 s+ after boot) for the
  performance counters; they now start loading at app launch, so GPU and disk arrive sooner too.
- Hardware: the Sensors tab opens with every card collapsed (each header already shows its key
  stats) instead of CPU and GPU expanded.

## [0.1.0] - 2026-09-30

### Added

- Web console: a new, read-only way to watch this PC's live stats from a browser on another device
  (`docs/specs/21-web-console.md`). Turned on from the new "Web console" page (off by default), it
  serves a small stats page - CPU, memory, GPU, disk and network with sparklines, hardware
  temperatures, busiest apps, drives, pending restart and system info - on a chosen port (default
  8765). Only GET/HEAD requests are accepted and no endpoint can change anything on the PC; stats
  require a random access key carried in the link's #fragment, which can be regenerated to lock out
  old links. Built on a minimal `TcpListener` server with strict request limits, so it needs no admin
  rights and adds no dependencies; stats are sampled only while a browser is watching.

- Lighting: custom animations. A new "Custom animations" card on the Lighting page imports your own
  LED animations - "Import animation file..." for a `.json` file, or "Paste from clipboard" for an
  animation an AI chat wrote - and "Copy AI prompt" copies a ready-made prompt template that teaches
  any AI chat the format, so you only describe what you want. Animations are data only
  (`porchlight-animation` JSON: `fill`, `gradient` or palette pixel-art `rows` frames, per-frame
  durations, loop, cut/fade transitions - never code), strictly validated with a plain-language
  error you can paste back to the AI, and stored in `%APPDATA%\Porchlight\Animations`. Pick
  "Custom animation" as any device's effect, choose the animation and a speed. Guide, prompt and
  examples: `docs/custom-animations.md`, `docs/animations/`; design: `docs/specs/20-custom-animations.md`.
- Hardware: Fans tab polish from maintainer testing on an ASUS ROG STRIX B550-F with Armoury Crate
  (`docs/specs/04-hardware-fans.md` addendum). Empty motherboard fan headers (never once reported
  RPM > 0) are now hidden when "Hide unused sensors" is on, reusing the Sensors tab's existing
  tracker/setting; a GPU fan is never hidden regardless of RPM history and shows "Stopped (idle)"
  instead of "0 RPM" for its legitimate 0-RPM idle mode. Fans can now be given a custom display
  name (inline "Rename" on the Fans tab), persisted keyed by the fan's stable controller id and
  shown everywhere a fan name appears (Fans tab, Sensors tab RPM rows, the "Hottest fan" summary
  tile), with the original hardware-reported name kept as subtitle text. The failsafe temperature
  slider and its label now show "°C" instead of a bare "C". Added detection (not prevention) of
  known vendor fan-control software - Armoury Crate/AsusFanControlService, MSI Center/Dragon
  Center, Gigabyte SIV/Control Center, FanControl, SpeedFan, Argus Monitor, iCUE, NZXT CAM, Lian Li
  L-Connect - running alongside Porchlight, with a caution banner on the Fans tab when one is
  found, re-checked whenever the Fans tab is opened. Fan-control actions (arming, pausing/restoring
  fans on exit or via "Restore BIOS control", a user changing a fan's mode or target duty, a
  detected conflict) are now logged at Information level; previously only failures were logged.

- Lighting: an HSV color wheel picker (`Porchlight.App.Controls.ColorWheelPicker`) replaces the
  plain hex box for both "Apply to all" and per-device color (a popup next to each device row) -
  drag or click the hue/saturation wheel, a value (brightness-of-the-hue) slider, a live preview
  swatch, and a hex/RGB box kept in sync both ways. The wheel is rendered once per pixel size into
  a cached `WriteableBitmap`, not per-pixel UI elements. Keyboard accessible (arrow keys adjust
  hue/saturation, visible focus ring, `AutomationProperties` names). Dragging pushes color to
  OpenRGB throttled to ~20/s (`Porchlight.Core.Lighting.ColorApplyRateLimiter`), always sending the
  final value on release. Favorite colors (up to 8, persisted) still work unchanged. Color math
  (HSV<->RGB, wheel-point<->hue/saturation) lives in testable `HsvColor`/`ColorWheelMath`.
  (`docs/specs/05-lighting.md` addendum.)
- Lighting: a dismissible (per app session) warning panel on the Lighting page detects other
  software that can also claim an RGB device's lighting - Windows Dynamic Lighting (with a specific
  message when its brightness is 0%) and vendor RGB apps (Logitech G HUB, Razer Synapse, Corsair
  iCUE, SteelSeries GG, ASUS Armoury Crate/Aura, MSI Mystic Light/Center, Gigabyte RGB Fusion,
  SignalRGB, NZXT CAM, HyperX NGenuity) - with plain-words advice and, for Windows Dynamic Lighting,
  a button that opens `ms-settings:personalization-lighting`. Purely informational: nothing is
  changed or stopped. New `ILightingConflictDetector`/`LightingConflictDetector` in Core.
  (`docs/specs/05-lighting.md` addendum.)
- Lighting: a per-device "Don't control this device" toggle (persisted) excludes a device from
  "Apply to all"/"Turn off all" - e.g. to leave a keyboard to its vendor's own software - without
  affecting devices that aren't excluded (a fast-path bulk call is still used when nothing is
  excluded). (`docs/specs/05-lighting.md` addendum.)
- Lighting: the page now auto-reconnects to OpenRGB (retrying every 10s) after a disconnect,
  instead of requiring the user to click Retry once OpenRGB is reachable again.
  (`docs/specs/05-lighting.md` addendum.)
- Hardware: redesigned the Sensors tab for readability (`docs/specs/10-readable-sensors.md`) - an
  "At a glance" summary strip (CPU/GPU temperature, CPU package power, hottest fan) above one card
  per device (CPU, GPU, motherboard, memory, storage, network; CPU/GPU expanded by default), each
  grouped into sections (temperatures, fans, load, power, clocks, voltages, then the rest) with
  aligned Name/Current/Min/Max columns across every card. Current is now the primary/bold text and
  Min/Max secondary - the previous tree view inherited a dim foreground from its `TreeViewItem`
  container for the Name/Current columns, the opposite of the intended emphasis. Every
  `SensorType` now has an explicit, correct unit (`SensorFormatter`) - including the Factor type,
  which previously rendered with no unit at all (the maintainer's "Core #1  34" row was a per-core
  multiplier sensor). Added a "Hide unused sensors" toggle (on by default, persisted) that hides a
  sensor whose value has been null or exactly zero for its entire observed history (unconnected fan
  headers, unpopulated voltage rails) while never hiding a temperature that has reported a real
  value.
- Hardware: collapsed device cards on the Sensors tab now show the device's 2 most useful live
  stats next to its name (e.g. `AMD Ryzen 7 5800X3D      69.8 °C · 22 %`), hidden once the card is
  expanded since the same values are then visible below (`docs/specs/10-readable-sensors.md`
  addendum). The pair is picked per hardware type by the new `HardwareCardStatsSelector`: CPU
  temperature + CPU Total load, GPU core temperature + GPU core load, memory load % + memory used
  GB, storage temperature + used space %, motherboard hottest temperature + fastest fan RPM,
  network download + upload throughput. Values update live and are included in the header's
  accessible name while collapsed.

### Fixed

- Dashboard: stats appear almost immediately on opening instead of after ~2 s (5 s+ right after a
  reboot). The first sample no longer waits a full interval, and the slow one-time performance-counter
  setup runs in the background, so memory, network, processes and drives show at once while CPU, GPU
  and disk join when it finishes; busiest-app CPU% is real a second sooner. OpenRGB auto-start no
  longer delays the main window.
- Hardware: CPU temperature and package power no longer show "0 °C / 0 W - Normal" when the sensor
  cannot be read (an AMD CPU with Porchlight not running as administrator); a zero reading is treated
  as no reading, so the tile is hidden, and alerts, checkup and the CPU temperature effect ignore it.
- Lighting: in the Pac-Man effect, Pac-Man now chases the ghost (it flees ahead of him and is caught
  at the end of the path) instead of the ghost chasing Pac-Man.
- Web console: the footer line sits under the cards instead of at the far left on wide screens.
- Updates: five fixes from manual testing of the friendly-outcomes/Reinstall work
  (`docs/specs/09-friendly-update-outcomes.md`'s addendum):
  - A row's last failed outcome (and its Reinstall.../Hide/Try again actions) now survives an app
    restart instead of only showing for the session that produced it - persisted per package id +
    the available version it was attempted against (`UpdatesSettings.LastOutcomes`,
    `UpdateOutcomeMemory`), cleared on a successful update/reinstall or once a newer version
    appears, and pruned to whatever winget currently lists.
  - "Not available for this PC" (`APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE`, e.g. RARLab.WinRAR
    refusing 6.24 -> 7.23) now offers **Reinstall...** alongside **Hide this update** -
    `WingetSuggestedAction` became a `[Flags]` enum so a row can offer more than one action.
  - "Close the app and try again" for an install-in-use failure now names the actual programs
    holding the app's files open, via the Windows Restart Manager API, when they aren't the app
    itself (observed for OBS Studio: not running, but Chrome and another app held its
    virtual-camera DLL open) - falls back to the previous generic text if the lookup finds nothing.
  - A silent, unusually long-running update now explains itself the moment winget's own output says
    it's about to raise a UAC prompt ("Waiting for your permission - look for the Windows prompt on
    the taskbar"), rather than only after two minutes of unexplained silence - a prompt raised from
    winget's background process can appear only as a flashing taskbar icon (observed for
    Google.CloudSDK).
  - A package whose installed version is "Unknown" (`--include-unknown`) that reports a successful
    update no longer reappears and gets updated again forever - Porchlight can't confirm the old
    install was actually replaced (observed for Google.CloudSDK: a second, per-user copy was
    installed alongside an already-current machine-wide one), so it's now remembered as "already
    updated" and hidden (still visible via "Show ignored") until a newer version appears. The Notes
    column for any unknown-version row now warns "updating may install a second copy".
- Lighting: fixed OpenRGB reliably exiting 5-10 seconds after Porchlight auto-started it
  (`--server --startminimized`), even though the identical command run manually from a shell
  stayed up indefinitely. `ProcessRunner.StartDetached` now launches through the shell
  (`UseShellExecute = true`, no stdio redirection, the returned `Process` never disposed/killed)
  instead of as Porchlight's own direct child - the standard fix for a "must outlive the launcher"
  detached process start on Windows. (`docs/specs/05-lighting.md` addendum, "OpenRGB auto-start
  reliability".)

### Verified

- Hardware: investigated the maintainer's screenshot where every CPU core's Load sensor read
  "100 %" Current at once. `HardwareService.BuildNode` maps `SensorReading.Value`/`Min`/`Max`
  straight from LHM's own `ISensor.Value`/`Min`/`Max` for every sensor, one-to-one, with no
  cross-sensor aggregation or copy-paste between rows, so Current cannot be bound to the wrong
  value there or in the new `SensorRowViewModel`/`SensorFormatter` display path. All-cores-100%
  is a real, momentary reading (something else was using the CPU when that screenshot was taken),
  not a bug; the demo data (`DemoHardwareService`) now shows genuinely mixed per-core load instead
  so this never looks like a rendering artifact again.

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
- Updates: friendlier update outcomes - the Status column always shows a short plain-language
  sentence (e.g. "Needs a reinstall", "Not available for this PC", "Close the app and try again")
  instead of a bare winget exit code; the code now only appears in the tooltip/`AutomationProperties.HelpText`
  and the log. Selecting a row shows a details panel with the action that fits its outcome:
  **Reinstall...** (behind a confirmation dialog) for the "install technology mismatch" case,
  running a new `ReinstallWorkflow` (uninstall, then install the newest version) via new
  `IWingetClient.UninstallAsync`/`InstallAsync` methods; **Hide this update** for "no applicable
  update"; and **Try again** / **Try install again** for retryable failures. A failed install after
  a successful uninstall is reported as a clearly critical "Not installed" state rather than being
  left ambiguous. The run summary is now plain words too, e.g. "Finished: 1 updated, 1 needs a
  reinstall, 1 not available for this PC". See `docs/specs/09-friendly-update-outcomes.md`.
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
