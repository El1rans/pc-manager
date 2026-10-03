# Features

A technical reference of what Porchlight does. For a friendlier walk through each page, see the [page guides](../README.md#pages).

- App shell with a navigation rail of seven entries - Overview, Tune-up (Updates, Startup apps, Free up space, Health check, Recent changes), Apps & services (Get apps, Running apps, Services, Remove apps), Internet & safety (Safety, Internet, Browser add-ons),
  Hardware (Sensors & fans, Lighting, Printers) Get help (Get help, Web console) and Settings (General, Notifications, Optional features; both of the last two are pinned at the bottom) - with a tab row above the page for categories that hold more than one, following
  Windows light/dark theme, with the app version shown in the sidebar footer.
- Browser add-ons page: read-only list of the add-ons installed in Edge, Chrome, Brave and Firefox
  (every profile), each with a plain-language note on what it can do (read all websites, see
  history, change proxy settings, ...), where it came from and a "Looks fine / Review / Worth
  removing" level. Porchlight never changes or removes anything in a browser; the page opens the
  browser's own add-ons page and explains how to remove one. Nothing leaves the PC.
  A "Start-up and search settings" block at the top of each browser's card (Chrome, Edge, Brave and
  Firefox, every profile) checks the home page, the pages that open at start-up, the new-tab page and
  the default search engine, and marks each "Looks fine", "Changed" (an unfamiliar address, shown by
  host name) or "Forced by a setting on this PC" (a browser policy in the registry points somewhere
  unfamiliar). A pure `BrowserHijackClassifier` with a known-good list does the judging; it is
  read-only, makes no network lookups, and a "Changed" result is advice, not a malware verdict.
  Firefox policies are not read. See `specs/32-browser-hijack-check.md`.
- Settings persisted as JSON under `%APPDATA%\Porchlight\settings.json`, atomic writes, corrupt-file
  recovery.
- Free up space: one Scan measures safe junk (temporary files, browser caches, crash reports, Windows Update leftovers, optionally the Recycle Bin), then one Clean up button removes the ticked items. Files in use are left alone, and the page never follows shortcuts or links into other folders. It also suggests big files and old downloads (moved to the Recycle Bin only when you click, so they can be restored) and large apps (opens the app's own uninstaller). Personal files are never deleted automatically.
  The same page has "What's using space?", a disk space map: pick your files, a drive or any folder, and Porchlight measures it in the background (never following shortcuts, and counting folders Windows won't let it read) and shows the biggest folders and files as a sorted list with proportional bars, sizes and percentages, with a breadcrumb to go back up and "Show in folder". Only files in your own folders can be moved to the Recycle Bin. "Duplicate files" finds identical copies of files 1 MB or bigger in your own folders (comparing sizes first, then the start and end of each file, then a full SHA-256 only where needed), shows how much space each set wastes and suggests "Keep newest"; you tick the copies to move to the Recycle Bin, and one copy of every set always stays. On the Dashboard, a drive that is low on space gets a "Free up space" button that opens this page. See `specs/19-disk-insights.md`.
- Logs written to `%APPDATA%\Porchlight\logs`.
- Admin elevation: the sidebar shows whether the app is running as administrator and can relaunch
  elevated.
- First-run setup: on first launch (and any time after, via Settings > Optional features, which also has a
  "Run first-time setup again" button), choose which optional third-party tools to install - AnyDesk (remote help), OpenRGB
  (lighting) and the PawnIO driver (hardware sensors/fan control). Already-installed tools are
  detected and skipped. Feature pages that need one of these show a shared status card and can
  install/start it in place, without restarting the app.
- Dashboard: live CPU, memory, GPU, disk and network usage with 60-second history sparklines,
  updated once a second (paused while the window is minimized). Drives with a low-space warning,
  top processes by CPU/memory, a pending-restart badge, and static system info (computer name, OS,
  manufacturer/model, CPU, GPU, RAM).
- Porchlight updates itself: an installed copy checks GitHub Releases (at startup unless the "Check
  for updates when Porchlight starts" option is off, and on Refresh) and shows "Porchlight X.Y.Z is
  available" at the top of the Updates page with a "What's new" link and an "Update now" button that
  downloads the installer, verifies its SHA-256, runs it (one Windows permission prompt) and restarts
  Porchlight on the new version. A copy that wasn't installed with the installer only gets a link to
  the release page. See `specs/23-self-update.md`.
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
- Check-up report (Get help page): "Send a check-up to <your helper>" builds a plain-language
  summary of this PC (computer and Windows, restarts, drive space, pending app updates,
  temperatures, remote help), shows exactly what it contains, and lets you copy it, save it as an
  HTML or text file, or open it in your mail program addressed to your helper (optional email
  setting). Porchlight never sends or uploads anything itself, and the report never includes your
  user name, files, installed apps, IP addresses or serial numbers. Other features add their own
  sections by registering an `ICheckupSection`; see `specs/16-checkup-report.md`.
- Move to a new PC (Updates page): "Save my app list..." exports installed apps with
  `winget export`; "Install apps from a list..." validates the file, shows the apps for
  confirmation, then runs `winget import` with the live log and progress.
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
  `upstream/openrgb-net.md` for why. The Lighting page's Effects card assigns a custom
  animated effect per device - Rainbow wave, Breathing, CPU temperature (color follows CPU
  temperature between a min/max °C), and, for a device with a matrix (per-key) zone, Pac-Man and
  Rain - plus a global "updates pending" overlay and a "Pause effects" button. See
  `specs/11-led-effects.md`. You can also add your own **custom animations**: click "Copy AI
  prompt", describe the animation you want to any AI chat, and paste its answer back with "Paste
  from clipboard" (or import a `.json` animation file) - then pick "Custom animation" for any
  device. See [`custom-animations.md`](custom-animations.md) for the guide, the prompt
  template and examples.
- Startup apps: lists everything that starts when you sign in - the per-user and all-users `Run`
  registry keys and both Startup folders - with a friendly name, publisher, On/Off status and a plain
  "What is this?" line, and marks Windows/Microsoft items and Porchlight's own tools (AnyDesk,
  OpenRGB) as "Recommended to keep". Turn items off and on exactly like Task Manager (the
  `StartupApproved` value): nothing is ever deleted, so it is always reversible, and no startup
  program is ever launched. Items for all users need administrator rights (see `specs/13-startup-apps.md`).
  Each item shows a High / Medium / Low startup impact from Windows' own StartupInfo measurements
  (Task Manager's thresholds: more than 1 s CPU or 3 MB disk is high, under 300 ms and 300 KB is low),
  with "Sort by impact"; reading them needs administrator rights, otherwise items show "Not measured".
  Scheduled tasks with a logon trigger (outside `\Microsoft\Windows\`, and not Porchlight's own) are
  listed as "Scheduled task" items and turned off/on through the task's Enabled flag only
  (see `specs/30-startup-impact-and-tasks.md`).
- Health check: a plain-language page with five cards - disk health (per physical disk: Healthy /
  Warning - back up your files soon / Unknown, from Windows' storage and failure-prediction data);
  Windows repair (`sfc /scannow`, then `DISM /RestoreHealth` offered only if SFC couldn't fix everything;
  needs administrator rights, takes 10-30 minutes, and can't be cancelled once started); restore point
  (create one, see the most recent ones, explains Windows' 24-hour limit and detects System Protection
  being off, with a link to open it); recent problems from the last 30 days of the Event Log (app
  crashes, blue screens, unexpected shutdowns, disk errors, failed updates - grouped and counted); and,
  on a laptop, battery wear. Any check that can't run shows "Couldn't check" instead of failing. See
  `specs/14-system-health.md`.
  A sixth, read-only Backup card says whether anything is backing up your files: File History (on or
  off, when it last ran) and OneDrive (signed in, and whether Desktop, Documents and Pictures are
  protected), plus an "Also found" line for well-known backup tools that are never judged. The
  verdict is Good (a backup in the last 7 days, or OneDrive protecting Desktop and Documents),
  Warning (out of date or partial) or Problem ("Nothing is backing up your files"), with buttons to
  open Windows' backup settings or OneDrive. It never changes a backup setting and also adds a
  "Backups" section to the check-up report. See `specs/33-backup-status.md`.

- Internet: connection status (Wi-Fi name and signal as bars and words, local IP, router, DNS),
  a guided "Fix my internet" check (network connection, router, website names, the internet) that
  suggests only matching fixes - clear saved website addresses, get a fresh connection, switch the
  connection off and on (administrator, with confirmation) - and only *offers* Windows' Network
  settings as a last resort; a Cloudflare speed test that only runs when you press the button (the
  last result is remembered); and a read-only list of programs using the internet, refreshed only
  while the page is open. The public IP address is never fetched. See `specs/18-network.md`.
- System tray icon and background alerts: a Porchlight icon in the notification area with a
  quick-stats tooltip (CPU, memory, free space on C:) and a menu (Open, Check for updates, Get help,
  Notifications settings, Exit). By default closing the window keeps Porchlight running in the
  tray (Exit really quits); starting it a second time just shows the running window. Plain-language
  balloon alerts for a nearly full drive, a sustained hot CPU/GPU (only when temperature readings are
  available), app updates ready, and a restart pending for over 3 days - each type can be turned
  off, each shown at most once a day, and clicking one opens the relevant page. App updates are
  checked on a schedule (every day by default, or weekly/never); Porchlight only looks, it never
  installs by itself. All of it is configured on the Settings > Notifications page (tray menu "Notifications settings" opens it). See `specs/15-tray-and-alerts.md`.

- Theme setting: "Match Windows" (default), "Light" or "Dark", chosen in the "Appearance" card on Settings > General and applied to every open window immediately. See
  `specs/25-theme-setting.md`.
- Web console (read-only): turn it on from the "Web console" page to watch this PC's live stats
  (CPU, memory, GPU, disk, network, temperatures, busiest apps, drives, system info) from a browser
  on your phone or another computer. It only shows information - there is no way to change anything
  on the PC from it - and it needs an access key, included in the link the page gives you (make a
  new key any time to lock out old links). Off by default; port 8765 unless you pick another. Use it
  on your home network, or through a VPN such as Tailscale when away - don't forward the port on
  your router. See `specs/21-web-console.md`.
- Update history (Updates page): every update, reinstall and install run from Porchlight (including
  Get apps) is logged with the version change, action, result and time, and shown grouped by day
  behind a "History" button, with "Clear history". Stored in its own `update-history.json` (last 500
  entries, atomic writes, a corrupt file is set aside instead of crashing). See `specs/26-update-history.md`.
- Get apps: searches the `winget` community catalog (`winget search --source winget`), marks apps
  that are already installed, and installs one app at a time with live progress and a plain-language
  result; a curated "Popular apps" list shows while the search box is empty. Only ids from the
  current results can be installed, and a running install is never cancelled. See `specs/27-get-apps.md`.
- Running apps: live list of running programs grouped by executable (Apps / Background / Windows),
  with CPU % and memory, refreshed every 2 s only while the page is shown, a filter and sorting.
  "End task" asks first, refuses Windows and critical processes and Porchlight itself, and checks
  each process's start time so a reused process id is never ended; "Open file location" selects the
  file in Explorer. See `specs/28-running-apps.md`.
- Services: lists services from other apps (Windows' own are hidden by default and read-only) with
  plain descriptions, publisher, status and start type, and can start, stop, restart or change the
  start type of third-party services when running as administrator. Driver services and Microsoft
  services are never changed; stopping a service with running dependents is refused. See
  `specs/29-windows-services.md`.
- Safety ("Is this PC safe?", Internet & safety): three cards that each load on their own.
  *Security software* reads antivirus and third-party firewall state from Windows Security Center
  (WMI `root\SecurityCenter2`, decoding the `productState` bitfield) and Windows' own firewall
  directly from the firewall policy COM object, and gives one verdict (protected, antivirus off,
  antivirus out of date, Windows Firewall off, or "Couldn't check") with a line per product.
  *Windows Update* shows when Windows last installed updates (from the Windows Update Agent history,
  ignoring Defender definition updates), failed attempts in the last 30 days, a pending restart, and
  a "Check now" that counts updates waiting (a search that can take minutes, so only on request and
  with a timeout). *Who can connect to this PC?* lists remote-access tools that are installed or
  running (TeamViewer, AnyDesk, RustDesk, UltraViewer, Splashtop, Chrome Remote Desktop, LogMeIn,
  ConnectWise/ScreenConnect, Supremo, Quick Assist), labels the AnyDesk Porchlight set up and warns
  calmly about the rest. Read-only; buttons only open Windows Security and Windows Update. See
  `specs/31-safety-status.md`.
- Recent changes (Tune-up): a journal of what Porchlight changed (startup items, services, cleanups,
  installs, updates, removed apps), newest first, with an Undo for the reversible ones (startup items, and
  service start types and start/stop) and "Can't be undone" for the rest. Before a batch of app updates and before a
  service's start type is changed, Porchlight asks Windows for a restore point when Settings >
  General > "Create a restore point before big changes" is on (default), System Protection is on and
  Windows' frequency limit allows it; a skipped or failed restore point never blocks the action. See
  `specs/34-recent-changes.md`.
- Remove apps (Apps & services): installed programs from the uninstall registry keys with name,
  publisher, size and install date, a filter and sorting. Remove asks first, then uses
  `winget uninstall --id ... --exact --silent` when winget knows the app, otherwise starts the
  program's own uninstaller. Runtimes and drivers sit in a collapsed "System parts - usually keep"
  group, AnyDesk/OpenRGB/PawnIO show "Managed by Porchlight" with no Remove button, Porchlight itself
  is never listed, and an "Often preinstalled" hint (never "bad") marks trial antivirus, game bundles
  and OEM helpers. Confirmed removals are recorded in Recent changes. See `specs/35-remove-apps.md`.
- Printers (Hardware): printers from WMI `Win32_Printer` with state as icon + text (Ready, Printing,
  Offline, Out of paper, Paper jam, Paused, Error), jobs waiting and network or direct connection;
  virtual printers (PDF, XPS, OneNote, Fax) are in a collapsed group. Per printer: Make default,
  Clear stuck print jobs (asks first), Print a test page, Use printer online. Page level: a guided
  "Fix my printer", Restart the print service (needs administrator) and Open printer settings. See
  `specs/36-printer-fixes.md`.
- Text size: Settings > General > Text size (Normal 100%, Large 125%, Extra large 150%) scales the
  page area immediately and at startup, stored as `Appearance.TextSize`. The navigation rail, tab
  strip, tray menu and notifications stay the same size, and pages reflow and scroll rather than
  clip at 900x600. See `specs/37-larger-text.md`.
- Check-up reminder: an optional tray balloon (Settings > Notifications, off by default) every week,
  2 weeks or month on a chosen weekday that nudges the person to send the check-up report. Clicking
  it opens Get help. It waits a full period after a report, shows a missed reminder once, and never
  sends anything. The check-up card shows "Last check-up" and "Next reminder". See
  `specs/38-checkup-reminder.md`.
- Web console, more views: read-only Security (`/api/security`), Updates waiting (`/api/updates`) and
  Startup impact (`/api/startup`) sections reuse the same services as the app, with a small menu to
  jump between sections. Every endpoint is GET-only; the access-key model is unchanged. See
  `specs/39-web-console-more.md`.
- Services: start type can also be "Starts with Windows (delayed)"; choosing plain "Starts with
  Windows" clears the delayed flag. See `specs/34-recent-changes.md`.
