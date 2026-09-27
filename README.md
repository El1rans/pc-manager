# PC Manager

A Windows app for monitoring and maintaining your PC: live system stats plus a toolbox of useful management tools.

Built with C# / .NET (WPF).

## Status

Foundation stage: a production-shaped app shell (solution layout, host + DI, logging, settings,
navigation with Dashboard/Updates/Hardware/Lighting placeholder pages, admin elevation support) is
in place under `src/`. `prototype/` holds the original PowerShell winget updater tool that
milestone 02 ports into the app; it is kept for reference and not built.

To run the prototype standalone, double-click `prototype/Winget Updater.bat`.

## Install

Download `PCManager-Setup-<version>.exe` from the [Releases](../../releases) page and run it.

- The installer is unsigned (see "Code signing" below), so Windows SmartScreen will warn that it
  "prevented an unrecognized app from starting". Click **More info**, then **Run anyway** to
  continue - this is expected for every release until the installer is code-signed.
- Setup asks for administrator rights (needed for a per-machine install and, if you choose the
  PawnIO driver below, its kernel driver), then lets you pick which optional components to set
  up. Each is installed with `winget`, so it needs an internet connection the first time:
  - **Remote help from family (AnyDesk)** - ticked by default. Lets a family member connect to
    help you, from the "Get help" page.
  - **RGB lighting control (OpenRGB)** - unticked by default. Needed for the Lighting page to
    control RGB devices (motherboard, RAM, GPU, keyboard, ...).
  - **Fan control and temperature sensors (PawnIO driver)** - unticked by default. Installs a
    signed kernel driver PC Manager's Hardware page needs for full sensor access and software fan
    control.
  - If `winget` is not available on your PC, Setup skips these and tells you so - PC Manager's own
    "Set up optional features" (in the sidebar) can install them later.
  - You can also choose to create a desktop shortcut and/or start PC Manager when anyone signs in
    to this PC.
- Uninstalling PC Manager (Windows Settings > Apps) does **not** remove AnyDesk, OpenRGB or the
  PawnIO driver - they are separate applications; uninstall them individually if you no longer
  need them. You will be asked whether to also delete PC Manager's settings and logs.
- For an unattended install, run the setup exe with `/VERYSILENT` and, to control which optional
  components are installed, `/COMPONENTS="anydesk,openrgb,pawnio"` (comma-separated ids from
  `installer/PCManager.iss`'s `[Components]` section; omit ids you don't want installed, or pass
  `/COMPONENTS=""` to install none of them).

See `docs/RELEASING.md` for how a new release is cut, and `docs/specs/07-installer.md` for the
installer's full design.

## Building, running, and testing

Requires the .NET SDK version pinned in `global.json` (Windows, since the app uses WPF).

```powershell
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project src/PCManager.App
```

See `CONTRIBUTING.md` for the full workflow and `docs/specs/` for the engineering standards and
milestone specs.

## Features

- App shell with a navigation rail (Dashboard, Updates, Hardware, Lighting, Get help), following
  Windows light/dark theme, with the app version shown in the sidebar footer.
- Settings persisted as JSON under `%APPDATA%\PCManager\settings.json`, atomic writes, corrupt-file
  recovery.
- Logs written to `%APPDATA%\PCManager\logs`.
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
  installs the selected apps one at a time with a live log and progress, "Silent install" and
  "Include apps with unknown version" options, and "Stop after current" to cancel the rest of a
  run. The nav badge shows how many updates are available; a check runs automatically at startup
  and on Refresh.
- Get help: a plain-language remote-support page for a non-technical user, pinned in its own group
  at the bottom of the nav rail. Installs AnyDesk, shows its address in large, selectable,
  grouped-digit text with a one-click "Copy address" (put the bare digits on the clipboard), a
  running/not-running status with a "Start AnyDesk" button, step-by-step instructions, a
  scam-safety warning, and "Copy support info" for sharing computer name/Windows version/address by
  message. PC Manager never changes any AnyDesk security setting.
- Hardware: a sensors tab (temperatures, fan speeds, load, clocks, voltages and power for CPU, GPU,
  motherboard, memory, storage and network, filterable, with min/max reset) and a fans tab (Default/
  Fixed/Curve control per fan, with a draggable-point curve editor). Software fan control is off by
  default, requires a one-time risk confirmation, and is only ever active while the page itself
  reports `Ready` (elevated, driver installed, hardware read healthy) - a fan profile enabled during
  an earlier elevated session never drives a fan on a later non-elevated launch. A safety engine
  enforces a minimum speed floor, an overheat failsafe, a lost/stale-sensor failsafe, a
  no-CPU-temperature failsafe, and restores every fan to BIOS control on a set failure, on exit, on
  system suspend, on session end, and on a crash. Needs administrator rights and the PawnIO driver
  (installed in place from the page) for full sensor access and fan control.
  **Important:** if PC Manager is forced to close, crashes, or the PC loses power while a fan is
  under software control, that fan stays at its last commanded speed - only restarting the PC (not
  relaunching PC Manager) hands it back to BIOS control. PC Manager warns about this before you turn
  software fan control on, and shows a banner at the next launch if it detects this happened.
- Lighting: control RGB devices (motherboard, RAM, GPU, keyboard, ...) through OpenRGB - apply a
  color and brightness to every device at once or to one device at a time, switch a device's mode,
  save up to 8 favorite colors, and load OpenRGB profiles. Shows the shared setup card until OpenRGB
  is installed and running, and an optional "Start OpenRGB with PC Manager" toggle; a dropped
  connection (including a silent remote close, caught by a periodic heartbeat) returns to a
  reconnect state instead of crashing the page. Talks to OpenRGB through a vendored, patched copy
  of `OpenRGB.NET` at `src/ThirdParty/OpenRGB.NET/` - see `THIRD-PARTY-NOTICES.md` and
  `docs/upstream/openrgb-net.md` for why.

## Planned modules

### Dashboard
- CPU/GPU temperatures (where the hardware exposes them)
- Battery health (laptops)

### App updates (winget)
- Choose which apps to update, ignore list, silent mode (from the prototype)
- Scheduled update checks with a tray notification
- Update history
- Install new apps from a search box; export/import an app list to set up a new PC

### Startup and processes
- Startup apps: see and disable what runs at boot, with startup impact
- Process list with CPU/RAM, kill or open file location
- Windows services viewer

### Cleanup and storage
- Temp files, Windows Update cache, recycle bin, browser caches
- Disk space map: find the biggest folders and files
- Duplicate file finder

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
- Everything logged to `%APPDATA%\PCManager\logs`
