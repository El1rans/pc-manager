# 34 - Recent changes, automatic restore point, delayed start (branch `feat/recent-changes`)

Porchlight changes things on the PC: startup items, services, app updates, cleanups. This milestone
keeps a plain list of those changes with Undo where it is possible, makes a restore point before the
riskier ones, and finishes the Services page's start-type choices.

## Goals

- **A. Change journal.** A "Recent changes" page (category `Tune-up`, `Order` 5, last in the group)
  lists what Porchlight changed, newest first: a plain sentence, the area ("Startup apps",
  "Services", "Free up space", "Apps", "Updates"), the time, and an **Undo** button where the change
  can be reversed. Changes that can't be undone say "Can't be undone"; undone ones say "Undone".
  Results of an undo are shown in plain words (success or what to do next).
- **B. Automatic restore point.** Before a batch of app updates (Updates page, "Update selected" /
  all) and before a service's start type is changed, Porchlight asks Windows for a restore point when
  the setting is on, System Protection is on and Windows' frequency limit allows it. A skipped or
  failed restore point never blocks the action. Setting: "Create a restore point before big changes"
  (Settings > General, default on).
- **C. Delayed start.** The Services page offers "Starts with Windows (delayed)" as a fourth start
  type, and choosing plain "Starts with Windows" clears the delayed flag.

## Non-goals

- Undo for cleanups, app installs or updates (shown as "Can't be undone").
- A "Remove apps" page (built elsewhere); the journal API is designed so recording an uninstall later
  is one line: `journal.Record(ChangeArea.Apps, "Removed Contoso Player")`.
- Restore points for single-service start/stop, or for single-app installs.

## Design

### Core (`Porchlight.Core.Changes`)

- `ChangeEntry` (`Id`, `Time`, `Area`, `Description`, `UndoType?`, `UndoPayload?`, `UndoneAt?`),
  `ChangeArea` (`Startup`, `Services`, `Cleanup`, `Apps`, `Updates`).
- `IChangeJournal` (singleton): `GetAll()` newest first, `Record(area, description, undoType?,
  undoPayload?)`, `UndoAsync(id, ct)`, `Changed` event. `ChangeJournal` stores
  `change-journal.json` under `AppDataPaths.Root` (so `PORCHLIGHT_DATA_DIR` isolates it), written
  atomically, lazy loaded, under one lock. A corrupt file is renamed `.bad` and the journal starts
  empty; a failed write keeps entries in memory; nothing throws into the UI.
- `ChangeJournalRules`: `MaxEntries = 500`, `MaxAgeDays = 90`; `Trim` drops the oldest.
- Undo registry: `IChangeUndoer { UndoType; UndoAsync(payload, ct) }`, registered by the feature that
  owns the change. The journal picks the undoer by `UndoType`; an entry with no type, an unknown type
  or a failed undo stays as it was ("can't be undone" / the failure message); a successful undo sets
  `UndoneAt`. Payloads are small JSON (`ChangeUndoPayload`).
- Undoers: `StartupChangeUndoer` (`startup.enabled`: lists, then sets the old on/off state),
  `ServiceStartTypeUndoer` (`services.starttype`: previous start type, delayed included),
  `ServiceStateUndoer` (`services.state`: start or stop again). Both service undoers list first
  because the service layer only changes names it listed in this session.
- `IAutoRestorePoint.EnsureAsync(reason, ct)` / `AutoRestorePoint`: setting off -> skip; one attempt
  per `AttemptCooldown` (10 min) per session; reads `IRestorePointService.GetStatusAsync`, skips
  when `RestorePointRules.Evaluate` says no (protection off, 24 h limit) or the status can't be read;
  otherwise `CreateAsync(reason, ModifySettings)`. Returns an outcome plus an optional quiet note
  ("A restore point was made first..." / "...System Protection is turned off."). Exceptions are
  logged and reported as `Failed`. Never throws.
- `ChangesSettings.CreateRestorePointBeforeBigChanges` (default true) in `AppSettings.Changes`.
- `ServiceManager.ToNative(ServiceStartType)` is the single start-type mapping (automatic ->
  `SERVICE_AUTO_START` + delayed false, delayed -> auto + delayed true); `ChangeServiceConfig2` with
  `SERVICE_CONFIG_DELAYED_AUTO_START_INFO` is written for every automatic choice.

### Wiring (each edit is small and additive)

| Caller | Records | Undo |
|---|---|---|
| Startup apps toggle | "Turned on/off X at startup" | yes (flip back) |
| Services start / stop | "Started/Stopped X" | yes (opposite) |
| Services restart | "Restarted X" | no |
| Services start type | "Set X to \"label\"" | yes (previous type) |
| Free up space clean | "Cleared N of junk files" | no |
| Get apps install | "Installed X" | no |
| Updates (batch, retry, reinstall) | "Updated X to 1.2" | no |

View models take `IChangeJournal?` / `IAutoRestorePoint?` as trailing optional constructor
parameters (existing tests keep compiling; the real container always supplies them, which
`AppCompositionTests` asserts). The restore point is requested in `UpdatesViewModel.UpdateSelectedAsync`
before the first package, and in `WindowsServicesViewModel` before a start-type change (only when
elevated: otherwise the change is refused anyway). The note, if any, goes to the update log / the
services message line.

### App (`Porchlight.App/Features/RecentChanges`)

`RecentChangesViewModel` (`Title` "Recent changes", `TuneUp`, `Order` 5), `RecentChangeRowViewModel`,
`RecentChangesView`, `AddRecentChangesFeature()` (registers `AddChangesCore()` and the page).
Status is never color alone (icon + text); works at 900x600; `AutomationProperties.Name` on Undo.
DEBUG demo mode uses `DemoChangeJournal` (in memory, sample rows).

## Tests

`ChangeJournalTests` (persistence across instances, newest first, cap at 500, 90-day age limit,
corrupt file, undo dispatch by type, failed/throwing/unknown undo, already undone);
`AutoRestorePointTests` (setting off, creates, protection off, 24 h limit, unreadable status, Windows
refuses, service throws, cooldown); `UndoerTests`; `ServiceManagerStartTypeTests` (delayed mapping);
view model tests for recording and restore-point gating (services, startup, updates), the four start
options, `RecentChangesViewModelTests`, the settings checkbox, and `AppCompositionTests` for the new
registrations. Fakes only: no real restore point or service is ever touched.

## Acceptance criteria

- [ ] Recent changes lists startup, service, cleanup, install and update changes newest first.
- [ ] Undo works for startup toggles and service start/stop/start type, with plain results; other
      entries say "Can't be undone".
- [ ] The journal survives restarts, is capped (500 / 90 days) and survives a corrupt file.
- [ ] A restore point is requested before an update batch and a service start-type change when the
      setting is on; a skipped or failed one never blocks the action.
- [ ] "Create a restore point before big changes" is on Settings > General, default on.
- [ ] "Starts with Windows (delayed)" can be chosen, and plain "Starts with Windows" clears it.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
