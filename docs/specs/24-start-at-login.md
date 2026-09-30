# 24 - Start at sign-in (branch `feat/start-at-login`)

Some sensors (AMD CPU temperature and power through LibreHardwareMonitor) are only readable when
Porchlight runs as administrator. Starting it by hand and accepting a UAC prompt every day is not
something a non-technical user will keep doing. This milestone adds an in-app option, "Start
Porchlight when I sign in to Windows", that starts Porchlight **hidden in the tray, as administrator,
with no UAC prompt at sign-in**.

## Goals

- One checkbox in the Notifications dialog turns it on or off; it shows the real state.
- At sign-in Porchlight starts elevated with no prompt, and shows only its tray icon.
- Turning it on or off asks for admin permission at most once (a UAC prompt), and declining leaves
  everything as it was, with a friendly message.

## Non-goals

- No change to the tray, alerts or the "keep running in the tray" behaviour beyond the `--tray` start.
- No per-user vs all-users choice: the task is for the current user only.
- No self-update of an installed copy; the task only follows the exe that is running.

## Design

- **A Task Scheduler task, not a Run key.** A Run entry cannot start a program elevated without a
  prompt; a task with `RunLevel = HighestAvailable` and an interactive-token logon can. It is
  registered with `schtasks.exe /Create /TN "Porchlight" /XML <file> /F`, removed with
  `schtasks.exe /Delete /TN "Porchlight" /F`, and read with `schtasks.exe /Query /TN "Porchlight" /XML`.
- **Task definition** (`Porchlight.Core/Startup/LoginLaunchTaskXml.cs`, built with `XElement` so paths
  and user names are XML-escaped): a `LogonTrigger` for the current user only (`DOMAIN\user`, 10 s
  delay so Explorer's tray exists), principal `InteractiveToken` + `HighestAvailable`, action = the
  running exe (`Environment.ProcessPath`) with argument `--tray` and its folder as working directory.
  Settings: runs on battery, no time limit (`PT0S`), `IgnoreNew`, hard terminate allowed,
  `StartWhenAvailable` off, enabled, and **`Priority` 4** - Task Scheduler's default of 7 would run
  Porchlight at below-normal priority.
- **`ILoginLaunchService`** (`Porchlight.Core/Startup`): `GetStateAsync` (parses command/arguments out
  of the queried XML; non-zero exit = no task), `EnableAsync`, `DisableAsync` (no-op success if the
  task is absent, so no UAC prompt for nothing) and `RefreshStaleRegistrationAsync`. Queries and
  elevated changes run through the existing `IProcessRunner` (hidden, no console window).
- **Elevation.** If Porchlight is already elevated (`IElevationService.IsElevated`) `schtasks` is
  run directly. Otherwise it goes through the new `IElevatedCommandRunner`
  (`Verb = "runas"`, `UseShellExecute`, hidden window, waits for exit) which raises one UAC prompt.
  Declining (Win32 error 1223) returns `Declined`, not an exception; the checkbox reverts and shows
  "Windows didn't get permission, so nothing was changed...". Any other failure reverts it too with a
  generic message (details go to the log).
- **Keeping the path fresh.** `LoginLaunchRefreshHostedService` runs once at startup on a pool
  thread, best-effort and logged. If the task exists but its command differs (case-insensitive) from
  the current exe - the app moved or was reinstalled elsewhere - and Porchlight is elevated it
  re-registers the task; if not elevated it only logs (it never prompts on its own).
- **`--tray`** (`App.OnStartup`): the main window is still created (so tray "Open", navigation and
  `WhiteFlashGuard` work exactly as when hidden to the tray - the guard attaches on first `Show`, which
  now simply happens later) but is not shown. The tray icon is started first; **if it is not visible
  the window is shown anyway**, so the app is never invisible and unreachable. The first-run setup
  wizard still opens when `FirstRunCompleted` is false. `MainViewModel.InitializeAsync` no longer
  resets an existing selection, so a tray action (e.g. "Get help") taken before the window's first
  `Loaded` is not overwritten by the initial "select first category".
- **Single instance.** A second launch (double-click, tray shortcut, the sign-in task racing a manual
  start) signals the running instance to show itself and exits (`SingleInstanceGuard`), and the task's
  `IgnoreNew` prevents overlapping runs.
- **UI** (`NotificationsWindow`): a "When I sign in" card under "When I close the window" with the
  checkbox and the line "Starts in the tray, as administrator, so every sensor works. Turning this on
  or off asks for admin permission once." The state is read when the dialog opens (checkbox disabled
  until then); toggling runs on a pool thread, disables the box meanwhile and shows any error inline.
  It is deliberately not stored in `settings.json`: the task is the source of truth.
- **Installer.** `[UninstallRun]` runs `schtasks.exe /Delete /TN "Porchlight" /F` hidden on uninstall
  (Setup is already elevated). The optional `{commonstartup}` "Start Porchlight when anyone signs in"
  shortcut is unchanged. It starts a non-elevated, window-showing Porchlight for every user, so with
  both enabled the shortcut and the task race: whichever starts second finds the first running and
  just asks it to show itself (the tray-started window would then appear). The two are independent;
  users who want the elevated tray start should leave the shortcut unticked.
- **Trust.** The task only launches the same signed exe with `--tray`; no network is involved.
  `docs/CODE_SIGNING_POLICY.md` lists `schtasks.exe` among the processes Porchlight starts.

## Acceptance criteria

- [ ] The Notifications dialog has "Start Porchlight when I sign in to Windows" with the secondary
      line above; it reflects whether the task exists when the dialog opens.
- [ ] Turning it on registers the task (one UAC prompt if not elevated, none if elevated); turning it
      off deletes it. The checkbox is disabled while working.
- [ ] Declining UAC, or any failure, reverts the checkbox and shows an inline message.
- [ ] The task XML has `HighestAvailable`, `InteractiveToken`, Priority 4, no time limit, runs on
      battery, `IgnoreNew`, a `LogonTrigger` for the current user, and runs the current exe with `--tray`.
- [ ] At the next sign-in Porchlight is running elevated with only its tray icon, no window, and no
      UAC prompt; tray "Open" shows the window normally (no white flash).
- [ ] `--tray` with no tray icon shows the window instead; `--tray` on first run still shows setup.
- [ ] If the task's path is stale and Porchlight is elevated, startup re-registers it; if not, only logs.
- [ ] Uninstalling removes the task.
- [ ] Unit tests (fakes only, nothing registered on the machine): XML content and escaping; enable /
      disable / query / refresh flows for elevated, non-elevated, declined and failed cases; the view
      model's load, toggle, revert-on-failure; `InitializeAsync` keeping an earlier selection.
- [ ] CHANGELOG entry; `dotnet build -c Release` 0 warnings; tests pass.
