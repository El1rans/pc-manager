# PC Manager

A Windows app for monitoring and maintaining your PC: live system stats plus a toolbox of useful management tools.

Built with C# / .NET (WPF).

## Status

Foundation stage: a production-shaped app shell (solution layout, host + DI, logging, settings,
navigation with Dashboard/Updates/Hardware/Lighting placeholder pages, admin elevation support) is
in place under `src/`. `prototype/` holds the original PowerShell winget updater tool that
milestone 02 ports into the app; it is kept for reference and not built.

To run the prototype standalone, double-click `prototype/Winget Updater.bat`.

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

- App shell with a navigation rail (Dashboard, Updates, Hardware, Lighting), following Windows
  light/dark theme.
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
