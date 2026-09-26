# PC Manager

A Windows app for monitoring and maintaining your PC: live system stats plus a toolbox of useful management tools.

Built with C# / .NET (WPF).

## Status

Early stage. `prototype/` holds the first working tool: a winget updater that lists available updates and lets you choose which ones to install.

To run the prototype, double-click `prototype/Winget Updater.bat`.

## Planned modules

### Dashboard
- Live CPU, RAM, GPU, disk and network usage with small history graphs
- CPU/GPU temperatures (where the hardware exposes them)
- Uptime, Windows version, last boot time, pending-restart indicator
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
