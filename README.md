<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/brand/porchlight-logo-dark.png">
  <img alt="Porchlight — We leave the light on for you." src="assets/brand/porchlight-logo-light.png" width="520">
</picture>

Porchlight is for the family member who ends up as tech support for a parent's or grandparent's PC
from a distance. It sits quietly on their computer, shows what's going on in plain language, and
makes it easy to check in, help out, and offer remote support when something needs a closer look -
so you can help before a small problem turns into a phone call.

Built with C# / .NET (WPF).

## Status

Foundation stage: a production-shaped app shell (solution layout, host + DI, logging, settings,
navigation with Dashboard/Updates/Hardware/Lighting placeholder pages, admin elevation support) is
in place under `src/`. `prototype/` holds the original PowerShell winget updater tool that
milestone 02 ports into the app; it is kept for reference and not built.

To run the prototype standalone, double-click `prototype/Winget Updater.bat`.

## Install

Download `Porchlight-Setup-<version>.exe` from the [Releases](../../releases) page and run it.

- The installer is currently unsigned (see "Code signing" below), so Windows SmartScreen will warn
  that it "prevented an unrecognized app from starting". Click **More info**, then **Run anyway**
  to continue - this is expected for every release until SignPath approves code signing for this
  project.
- Setup asks for administrator rights (needed for a per-machine install and, if you choose the
  PawnIO driver below, its kernel driver), then lets you pick which optional components to set
  up. Each is installed with `winget`, so it needs an internet connection the first time:
  - **Remote help from family (AnyDesk)** - ticked by default. Lets a family member connect to
    help you, from the "Get help" page.
  - **RGB lighting control (OpenRGB)** - unticked by default. Needed for the Lighting page to
    control RGB devices (motherboard, RAM, GPU, keyboard, ...).
  - **Fan control and temperature sensors (PawnIO driver)** - unticked by default. Installs a
    signed kernel driver Porchlight's Hardware page needs for full sensor access and software fan
    control.
  - If `winget` is not available on your PC, Setup skips these and tells you so - Porchlight's own
    "Set up optional features" (in the sidebar) can install them later.
  - You can also choose to create a desktop shortcut and/or start Porchlight when anyone signs in
    to this PC.
- Uninstalling Porchlight (Windows Settings > Apps) does **not** remove AnyDesk, OpenRGB or the
  PawnIO driver - they are separate applications; uninstall them individually if you no longer
  need them. You will be asked whether to also delete Porchlight's settings and logs.
- For an unattended install, run the setup exe with `/VERYSILENT` and, to control which optional
  components are installed, `/COMPONENTS="anydesk,openrgb,pawnio"` (comma-separated ids from
  `installer/Porchlight.iss`'s `[Components]` section; omit ids you don't want installed, or pass
  `/COMPONENTS=""` to install none of them).
- **Upgrading from PC Manager?** Porchlight is the renamed version of the same app (same install,
  same data, nothing to redo). Installing over an existing PC Manager install moves it to
  `Program Files\Porchlight`, and your settings are migrated automatically from
  `%APPDATA%\PCManager` to `%APPDATA%\Porchlight` the first time Porchlight runs - the old folder
  is left in place, untouched.

See `docs/RELEASING.md` for how a new release is cut, and `docs/specs/07-installer.md` for the
installer's full design.

### Code signing policy

Porchlight has applied for free code signing through the
[SignPath Foundation](https://signpath.org/) program for open source projects. Signing is being
set up: `.github/workflows/release.yml` is wired to submit each tagged release for signing, but
releases are signed only once SignPath approves the application and the maintainer finishes the
one-time project setup - see [`docs/CODE_SIGNING_POLICY.md`](docs/CODE_SIGNING_POLICY.md) for the
full policy (team roles, privacy statement, what Porchlight contacts over the network) and
`docs/RELEASING.md` for the setup steps. Until then, every release stays unsigned and SmartScreen
will warn as described above.

Free code signing provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

## Building, running, and testing

Requires the .NET SDK version pinned in `global.json` (Windows, since the app uses WPF).

```powershell
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project src/Porchlight.App
```

See `CONTRIBUTING.md` for the full workflow and `docs/specs/` for the engineering standards and
milestone specs.

## Features

- App shell with a navigation rail (Dashboard, Updates, Hardware, Lighting, Free up space, Get help), following
  Windows light/dark theme, with the app version shown in the sidebar footer.
- Settings persisted as JSON under `%APPDATA%\Porchlight\settings.json`, atomic writes, corrupt-file
  recovery.
- Free up space: one Scan measures safe junk (temporary files, browser caches, crash reports, Windows Update leftovers, optionally the Recycle Bin), then one Clean up button removes the ticked items. Files in use are left alone, and the page never follows shortcuts or links into other folders. It also suggests big files and old downloads (moved to the Recycle Bin only when you click, so they can be restored) and large apps (opens the app's own uninstaller). Personal files are never deleted automatically.
  The same page has "What's using space?", a disk space map: pick your files, a drive or any folder, and Porchlight measures it in the background (never following shortcuts, and counting folders Windows won't let it read) and shows the biggest folders and files as a sorted list with proportional bars, sizes and percentages, with a breadcrumb to go back up and "Show in folder". Only files in your own folders can be moved to the Recycle Bin. "Duplicate files" finds identical copies of files 1 MB or bigger in your own folders (comparing sizes first, then the start and end of each file, then a full SHA-256 only where needed), shows how much space each set wastes and suggests "Keep newest"; you tick the copies to move to the Recycle Bin, and one copy of every set always stays. On the Dashboard, a drive that is low on space gets a "Free up space" button that opens this page. See `docs/specs/19-disk-insights.md`.
- Logs written to `%APPDATA%\Porchlight\logs`.
- Admin elevation: the sidebar shows whether the app is running as administrator and can relaunch
  elevated.
- First-run setup: on first launch (and any time after, via "Set up optional features" in the
  sidebar), choose which optional third-party tools to install - AnyDesk (remote help), OpenRGB
  (lighting) and the PawnIO driver (hardware sensors/fan control). Already-installed tools are
  detected and skipped. Feature pages that need one of these show a shared status card and can
  install/start it in place, without restarting the app.
- Dashboard: live CPU, memory, GPU, disk and network usage with 60-second history sparklines,
  updated once a second (paused while the window is minimized). Drives with a low-space warning,
  top processes by CPU/memory, a pending-restart badge, and static system info (computer name, OS,
  manufacturer/model, CPU, GPU, RAM).
- Updates page: lists `winget upgrade` results with selection, filtering and an ignore list;
  installs the selected apps one at a time with a live log and progress, "Silent install",
  "Include apps with unknown version" and "Check for updates when Porchlight starts" options, and
  "Stop after current" to cancel the rest of a run. The nav badge shows how many updates are
  available; a check runs automatically at startup (unless turned off) and on Refresh. Every
  outcome is a plain-language status with a matching action - Reinstall... (uninstall then install
  the newest version), Hide this update, or Try again - that's remembered across app restarts; an
  "app in use" failure names the actual programs holding the app's files open when it isn't the app
  itself; and a long silent wait explains itself the moment winget says it's about to raise a
  Windows permission prompt.
- Get help: a plain-language remote-support page for a non-technical user, pinned in its own group
  at the bottom of the nav rail. Installs AnyDesk, shows its address in large, selectable,
  grouped-digit text with a one-click "Copy address" (put the bare digits on the clipboard), a
  running/not-running status with a "Start AnyDesk" button, step-by-step instructions, a
  scam-safety warning, and "Copy support info" for sharing computer name/Windows version/address by
  message. Porchlight never changes any AnyDesk security setting.
- Hardware: a sensors tab with an "At a glance" summary strip (CPU/GPU temperature, CPU package
  power, hottest fan) above one card per device (CPU, GPU, motherboard, memory, storage, network -
  CPU/GPU expanded by default), each grouped into sections (temperatures, fans, load, power,
  clocks, voltages, then the rest) with aligned Name/Current/Min/Max columns - Current is the
  prominent value, Min/Max are secondary. A "Hide unused sensors" toggle (on by default) hides
  sensors that have never reported a real reading (unconnected fan headers, unpopulated voltage
  rails); a filter box and "Reset min/max" still work across every card. A fans tab (Default/
  Fixed/Curve control per fan, with a draggable-point curve editor) sits alongside it - empty
  motherboard fan headers that have never reported RPM are hidden the same way (GPU fans are never
  hidden, since a 0-RPM idle mode is normal for them - shown as "Stopped (idle)"), each fan can be
  given a custom display name (shown everywhere its name appears), and a banner warns when another
  fan-control tool (Armoury Crate, MSI Center, iCUE, ...) is detected running alongside Porchlight.
  Software fan control is off by
  default, requires a one-time risk confirmation, and is only ever active while the page itself
  reports `Ready` (elevated, driver installed, hardware read healthy) - a fan profile enabled during
  an earlier elevated session never drives a fan on a later non-elevated launch. A safety engine
  enforces a minimum speed floor, an overheat failsafe, a lost/stale-sensor failsafe, a
  no-CPU-temperature failsafe, and restores every fan to BIOS control on a set failure, on exit, on
  system suspend, on session end, and on a crash. Needs administrator rights and the PawnIO driver
  (installed in place from the page) for full sensor access and fan control.
  **Important:** if Porchlight is forced to close, crashes, or the PC loses power while a fan is
  under software control, that fan stays at its last commanded speed - only restarting the PC (not
  relaunching Porchlight) hands it back to BIOS control. Porchlight warns about this before you turn
  software fan control on, and shows a banner at the next launch if it detects this happened.
- Lighting: control RGB devices (motherboard, RAM, GPU, keyboard, ...) through OpenRGB - apply a
  color and brightness to every device at once or to one device at a time, switch a device's mode,
  save up to 8 favorite colors, and load OpenRGB profiles. Shows the shared setup card until OpenRGB
  is installed and running, and an optional "Start OpenRGB with Porchlight" toggle; a dropped
  connection (including a silent remote close, caught by a periodic heartbeat) returns to a
  reconnect state instead of crashing the page. Talks to OpenRGB through a vendored, patched copy
  of `OpenRGB.NET` at `src/ThirdParty/OpenRGB.NET/` - see `THIRD-PARTY-NOTICES.md` and
  `docs/upstream/openrgb-net.md` for why. The Lighting page's Effects card assigns a custom
  animated effect per device - Rainbow wave, Breathing, CPU temperature (color follows CPU
  temperature between a min/max °C), and, for a device with a matrix (per-key) zone, Pac-Man and
  Rain - plus a global "updates pending" overlay and a "Pause effects" button. See
  `docs/specs/11-led-effects.md`.
- Startup apps: lists everything that starts when you sign in - the per-user and all-users `Run`
  registry keys and both Startup folders - with a friendly name, publisher, On/Off status and a plain
  "What is this?" line, and marks Windows/Microsoft items and Porchlight's own tools (AnyDesk,
  OpenRGB) as "Recommended to keep". Turn items off and on exactly like Task Manager (the
  `StartupApproved` value): nothing is ever deleted, so it is always reversible, and no startup
  program is ever launched. Items for all users need administrator rights. No startup-impact rating
  and no scheduled tasks are shown (see `docs/specs/13-startup-apps.md`).

## Planned modules

### Dashboard
- CPU/GPU temperatures (where the hardware exposes them)
- Battery health (laptops)

### App updates (winget)
- Choose which apps to update, ignore list, silent mode (from the prototype)
- Scheduled update checks with a tray notification
- Update history
- Install new apps from a search box; export/import an app list to set up a new PC

### Processes and services
- Process list with CPU/RAM, kill or open file location
- Windows services viewer
- Startup impact ratings and logon scheduled tasks for the Startup apps page

### System health
- Run SFC / DISM, check disk (SMART) status
- Create a restore point before risky changes
- Recent crashes and errors from the Event Log in plain language

### Network
- Current IP, DNS, Wi-Fi signal, speed test
- Flush DNS, reset network adapter
- See which apps are using the network

### Quality of life
- System tray icon with quick stats
- Dark / light theme
- Everything logged to `%APPDATA%\Porchlight\logs`
