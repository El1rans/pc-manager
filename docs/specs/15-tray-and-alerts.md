# 15 - Tray icon, background alerts and scheduled update checks (branch `feat/tray-alerts`)

Porchlight is maintained remotely by a family member for a non-technical user. Today it only helps
while its window is open. This milestone lets it stay running quietly in the notification area,
tell the user in plain language when something needs attention (a nearly full drive, a hot
processor, waiting updates, a long-pending restart), and check for app updates on a schedule -
without ever installing anything by itself.

## Goals

- A notification-area (tray) icon with a tooltip of quick stats (CPU %, memory %, free space on
  C:), a right-click menu (Open Porchlight, Check for updates, Get help, Notifications settings,
  Exit) and double-click to open the window.
- "Keep Porchlight running in the tray when I close the window" (default on): closing the window
  hides it; Exit from the tray really quits. A one-time balloon explains "Porchlight is still
  running here".
- Background alerts shown as tray balloons (Windows renders them as toasts): low disk space,
  sustained high CPU/GPU temperature, updates available after a scheduled check, and a restart
  that has been pending for more than 3 days. Each alert type can be turned off, each fires at most
  once per 24 hours per subject (persisted), and clicking the balloon opens the relevant page.
- Scheduled update checks: "Check for app updates every day / week / never" (default daily),
  reusing the existing winget check path. Never installs anything.
- A small "Notifications" dialog with all of these toggles, opened from the tray menu and from a
  link in the sidebar footer.
- Alert decisions (thresholds, sustain windows, throttling) are a pure `Porchlight.Core` class with
  unit tests driven by a fake clock (`TimeProvider`).

## Non-goals

- No automatic installation of updates, ever. Alerts and scheduled checks only tell the user.
- No "start hidden in the tray" at Windows sign-in. "Start with Windows" stays the installer's
  optional Common Startup shortcut (`installer/Porchlight.iss`, task `startupicon`); the window
  still opens when Porchlight starts. A later change can add a `--tray` startup argument.
- No new NuGet package and no `UseWindowsForms` (see "Size").
- No custom toast content (buttons, images, `ToastNotificationManager`). A balloon is enough and
  needs no AppUserModelID registration or package identity.
- No sound, no per-drive thresholds, no user-editable temperature limits (named constants only).

## Design

### Core (`src/Porchlight.Core/Alerts/`, `Tray/`, `Settings/`)

- `NotificationSettings` (new `AppSettings.Notifications` section, additive - old settings files
  load with defaults): `KeepRunningInTray` (true), `TrayHintShown` (false), `AlertLowDisk`,
  `AlertTemperature`, `AlertUpdates`, `AlertRestartPending` (all true), `UpdateCheckSchedule`
  (`Daily` | `Weekly` | `Never`, default `Daily`, stored as a string), `LastScheduledUpdateCheckUtc`,
  `LastAlertUtc` (`Dictionary<string, DateTimeOffset>` keyed `"<kind>:<subject>"`) and
  `RestartPendingSinceUtc`.
- `AlertEvaluator` (pure logic, `TimeProvider` injected, state behind `IAlertStateStore`):
  - `Evaluate(AlertInputs, AlertPreferences)` returns the alerts to show now for low disk,
    temperature and pending restart; `EvaluateUpdates(count, preferences)` handles the updates alert
    (called only after a scheduled check finishes).
  - Low disk: any drive in `AlertInputs.Drives` with `DriveThresholds.IsLow`; subject = drive root.
  - Temperature: CPU above `AlertThresholds.CpuTemperatureC` (90 C) or GPU above
    `GpuTemperatureC` (85 C) continuously for `TemperatureSustain` (3 minutes). A reading of
    `null` (hardware needs admin + PawnIO; sensors unavailable) or a reading back under the
    threshold resets the sustain timer - so alerts silently never fire without readings.
  - Pending restart: the first time a restart is seen pending the time is persisted
    (`RestartPendingSinceUtc`); it fires once that is over `RestartPendingAfter` (3 days) old, and
    the marker is cleared when no restart is pending.
  - Throttle: an alert with the same key is suppressed for `Throttle` (24 hours) after it last
    fired; the fire time is persisted through `IAlertStateStore` (`SettingsAlertStateStore` over
    `ISettingsStore.Update`) so an app restart does not repeat it.
  - A disabled alert type never fires and never consumes its throttle window.
- `UpdateCheckPolicy.IsDue(schedule, lastCheckUtc, now)`: `Never` false; `Daily` after 24 h;
  `Weekly` after 7 days; never-checked is always due.
- `IAlertInputProvider` / `AlertInputProvider`: gathers fixed drives (`IDriveMonitor`, filtered to
  `DriveType.Fixed`), CPU/GPU temperature from `IHardwareService.Latest` using
  `HardwareSummarySelector` (only when the snapshot is fresh and has a value), and
  `IRestartDetector`. Every failure is logged and yields "no data", never an exception.
- `IQuickStatsProvider` / `QuickStatsProvider` (own `GetSystemTimes` / `GlobalMemoryStatusEx`, so it
  does not disturb the Dashboard's performance counters) and `TrayTooltipFormatter` (pure; fits
  the 127-char tooltip limit: "Porchlight - CPU 12%, memory 48%, C: 85 GB free").

### App (`src/Porchlight.App/Tray/`, `Features/Notifications/`, `Shell/`)

- `Tray/TrayIcon` (`ITrayIcon`): a small `Shell_NotifyIcon` wrapper (NOTIFYICONDATAW, version 4)
  hosted on a message-only `HwndSource`. Handles `NIM_ADD/MODIFY/DELETE`, `NIF_INFO` balloons,
  the registered `TaskbarCreated` message (re-adds the icon after an Explorer restart), left
  double-click, right-click/`WM_CONTEXTMENU` (native popup menu, so it needs no hidden WPF window)
  and `NIN_BALLOONUSERCLICK`. It removes the icon on dispose, on host shutdown, on
  `AppDomain.ProcessExit` and from the unhandled-exception handler. The icon is the exe's own
  icon (`ExtractIconEx`), so no extra resource is embedded.
- `Tray/TrayService`: owns the menu, refreshes the tooltip every few seconds off the UI thread,
  and maps menu/balloon/double-click actions to `IShellWindowService`.
- `Shell/IShellWindowService` / `ShellWindowService`: `ShowMainWindow()` (un-hide, restore from
  minimized, bring to front), `NavigateTo(...)`, `RequestExit()` (asks `IBusyGuard` pages as the
  close button does, then `IAppLifetime.Shutdown`), and the exiting flag.
- `MainWindow.Closing`: when `KeepRunningInTray` is on, the tray icon is present and the app is not
  exiting (menu Exit, session end, or explicit shutdown), the close is cancelled and the window is
  hidden instead; the first time, a balloon says "Porchlight is still running here. Right-click
  this icon to open it or quit." The busy-work confirmation is skipped when merely hiding (nothing
  stops) and kept for Exit.
- `Shell/SingleInstanceGuard`: there was no single-instance handling before (the named mutexes only
  let the installer detect the app). Because a hidden window would make a second launch look like
  "nothing happened", a second start now signals the running instance (named event) and exits; the
  running instance re-shows its window. Ownership is waited on for a few seconds so the elevated
  relaunch (`RestartElevated`) still works while the old process is shutting down. If the guard
  cannot be created or opened (for example a different elevation level) the app just runs normally.
- `Features/Notifications/`: `NotificationsWindow` + `NotificationsViewModel` (toggles, schedule
  picker, plain-language labels, persisted via `ISettingsStore.Update`), `AlertHostedService`
  (background loop every minute: gather inputs, `AlertEvaluator`, show balloons via `ITrayIcon`,
  remember the target page for the click), `ScheduledUpdateCheckHostedService` (checks
  `UpdateCheckPolicy` at start and hourly, runs the Updates page's existing `RefreshAsync` on the UI
  thread - never an install - then `EvaluateUpdates` with the non-ignored count) and
  `NotificationsFeature.AddNotificationsFeature()` (DI). The sidebar footer gets a
  "Notifications" link (`MainViewModel.OpenNotificationsCommand`).
- DEBUG demo mode (`DemoDataMode`): the tray icon and dialog work; background alerts and scheduled
  checks are off so demo drives/temperatures never raise real toasts.

### Alert text (plain, sentence case)

| Alert | Title | Message | Opens |
|---|---|---|---|
| Low disk | "Drive C: is almost full" | "Only 4.2 GB is free. Open Porchlight to see your drives." | Dashboard |
| Temperature | "Your PC is running hot" | "The processor has been very hot for a few minutes. Make sure the vents are not blocked. If this keeps happening, tell your helper." | Hardware |
| Updates | "3 app updates are ready" | "Open Porchlight to install them when you like." | Updates |
| Restart | "Your PC needs a restart" | "It has been waiting for 4 days. Restart when you have a moment so updates can finish." | Dashboard |

### Size

A `Shell_NotifyIcon` P/Invoke wrapper adds well under 20 KB of IL to the single-file exe.
`UseWindowsForms` would pull the WinForms assemblies (several MB in the self-contained publish)
and a tray package (e.g. Hardcodet.NotifyIcon.Wpf) adds an assembly plus its own rendering of the
context menu; both were rejected. Commit `2306253` (smaller build) is untouched: no new packages,
no trimming changes.

## Acceptance criteria

1. The tray icon appears at start, its tooltip shows CPU %, memory % and free space on C:, and it is
   removed on Exit, on a crash exit path, and re-added after Explorer restarts (`TaskbarCreated`).
2. Right-click shows Open Porchlight, Check for updates, Get help, Notifications settings, Exit;
   double-click opens the window; each item does what its label says.
3. With "Keep Porchlight running in the tray" on (default), closing the window hides it and the
   process keeps running; with it off, closing exits as before. Tray Exit always quits, after the
   same busy-work confirmation as before, and fans are restored via the normal host shutdown path.
4. Starting Porchlight a second time shows the running instance's window (even if hidden) and the
   second process exits.
5. The first hide shows the "still running here" balloon exactly once (persisted).
6. Low disk, sustained temperature, updates-available and restart-pending alerts fire per the rules
   above, at most once per 24 h per subject (surviving a restart), respect their toggles, never fire
   for temperatures when readings are unavailable, and clicking the balloon opens the right page.
7. A scheduled check runs when due (daily default), updates `LastScheduledUpdateCheckUtc`, never
   installs anything, and is skipped when set to Never or when an update run is in progress.
8. The Notifications dialog opens from the tray menu and the sidebar footer link and persists every
   toggle immediately.
9. `AlertEvaluator`, `UpdateCheckPolicy`, `TrayTooltipFormatter` and `NotificationsViewModel` have
   unit tests (fake `TimeProvider`); `dotnet build -c Release` has zero warnings and
   `dotnet test -c Release` passes.
