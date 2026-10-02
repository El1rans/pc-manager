# 26 - Update history (branch `feat/update-history`)

The Updates page forgets everything once a package is updated: a user who asks "what changed on my
PC last week?" or "did Zoom update?" has no answer. This milestone keeps a local log of every
update, reinstall and install Porchlight runs through winget, and shows it on the Updates page.

## Goals

- Record one history entry each time the Updates page finishes an update, a reinstall, or an
  install-only retry - successful or not. Cancelled-before-start packages are not recorded.
- A "History" button on the Updates page header switches the page body to the history list; a
  "Back to updates" button switches back. No new nav tab.
- The list is grouped by day ("Today", "Yesterday", then e.g. "Monday, 28 September"), newest first.
  Each row: app name, version change ("1.2.0 → 1.3.1", or just the new version when the old one is
  unknown), what was done ("Updated", "Reinstalled", "Installed"), result as icon + text ("Done",
  "Didn't work" + the friendly outcome title from spec 09), and the time.
- A failed row has the same friendly explanation the live row showed (tooltip or second line).
- "Clear history" with a confirmation ("Clear all update history? This can't be undone.").
- Empty state: "Nothing here yet. Updates you run from Porchlight will show up here."
- DEBUG demo mode (`PORCHLIGHT_DEMO_DATA=1`) shows a fake history.

## Non-goals

- Updates done outside Porchlight (Windows Update, apps updating themselves, winget in a terminal).
  Windows has no reliable cross-installer log of these; the page says "Updates run from Porchlight".
- "Move to a new PC" imports (a bulk winget import has no per-package result); not recorded.
- Roll back / downgrade from history. Exporting history.

## Design

### Core (`Porchlight.Core.Winget`)

- `UpdateHistoryAction` - `Update`, `Reinstall`, `Install`.
- `UpdateHistoryEntry` - plain mutable POCO (like `PersistedUpdateOutcome`): `TimestampUtc`
  (`DateTimeOffset`), `PackageId`, `PackageName`, `FromVersion?`, `ToVersion?`, `Action`,
  `Succeeded` (bool), `OutcomeTitle`, `Explanation`, `ExitCode`.
- `UpdateHistoryLog` - pure static helpers: `Append(list, entry)` keeps at most `MaxEntries = 500`
  (oldest dropped) and `GroupByDay(entries, DateTimeOffset now, TimeZoneInfo zone)` returning
  ordered `(string Label, IReadOnlyList<UpdateHistoryEntry>)` groups with the Today/Yesterday/date
  labels. Unit tested with a fixed clock and zone.
- `IUpdateHistoryStore` / `UpdateHistoryStore` (singleton): `IReadOnlyList<UpdateHistoryEntry> GetAll()`,
  `void Add(UpdateHistoryEntry)`, `void Clear()`. Stored in its own file
  `AppDataPaths.Root\update-history.json` (NOT `settings.json` - it grows and must not bloat or
  race with settings). Rules:
  - Lazy load on first use, cached in memory, every mutation under one lock.
  - Write atomically: write `update-history.json.tmp`, then `File.Move(tmp, path, overwrite: true)`.
  - A missing file = empty. A corrupt/unreadable file is logged at Warning, renamed to
    `update-history.json.bad` (overwriting an older `.bad`) and history starts empty - never throw
    into the UI.
  - A write failure is logged at Warning and the in-memory list is kept.
  - The path comes from `AppDataPaths.Root`, so DEBUG demo/test runs never touch the real file.
    Tests construct the store with an explicit temp folder path (add a constructor that takes the
    file path; the DI registration uses `AppDataPaths.Root`).
- `Demo/FakeUpdateHistoryStore` (DEBUG) with ~10 entries over the last week, registered by the
  existing Winget DI extension under demo mode.

### App (`Porchlight.App/Features/Updates`)

- Inject `IUpdateHistoryStore` into `UpdatesViewModel`. Add one private `RecordHistory(...)` helper
  and call it from the same three places that persist outcomes today
  (`PersistUpgradeOutcome`, `PersistReinstallOutcome`, `PersistInstallOnlyOutcome`) - including
  the success paths that currently call `UpdateOutcomeMemory.Forget`. `FromVersion` is the row's
  installed version, `ToVersion` the available version. Recording must never throw (wrap and log).
- `UpdateHistoryViewModel` (child VM owned by `UpdatesViewModel`, exposed as `History`):
  `Groups` (observable), `IsEmpty`, `RefreshCommand`-style `Load()`, `ClearCommand` (confirmation
  through the existing dialog/message service the app already uses; check how Updates/Cleanup
  confirm today and reuse it). `UpdateHistoryEntryViewModel` formats the row text.
- `UpdatesViewModel.IsShowingHistory` + `ShowHistoryCommand` / `HideHistoryCommand`; showing
  reloads the list. The History button stays enabled during an update run (the list is read-only,
  so viewing it mid-run is safe). Navigating away and back
  returns to the updates list.
- `UpdatesView.xaml`: header button "History" (glyph `` History), a second body
  (`Visibility` bound to `IsShowingHistory`) with group headers and rows in a scrolling list.
  Status is icon + text, never color alone. Keyboard reachable; `AutomationProperties.Name` on
  buttons and rows ("Zoom, updated to 6.2.1, done, 14:05").

## Tests

`UpdateHistoryLogTests` (cap at 500 dropping oldest; grouping labels Today/Yesterday/date across a
midnight boundary in a non-UTC zone; newest first); `UpdateHistoryStoreTests` against a temp folder
(missing file, round-trip, atomic write leaves no `.tmp`, corrupt file renamed to `.bad` and starts
empty, Clear); `UpdatesViewModelTests` additions (a successful update, a failed update and a
reinstall each add exactly one entry with the right versions/action/result; a cancelled run adds
none); `UpdateHistoryViewModelTests` (empty state, grouping, clear after confirm, no clear on
cancel).

## Acceptance criteria

- [ ] Every update/reinstall/install-only retry run from the Updates page adds one history entry,
      success or failure; cancelled-before-start packages add none.
- [ ] History is stored in `update-history.json` under `AppDataPaths.Root`, capped at 500, written
      atomically; a corrupt file never crashes the app.
- [ ] The Updates page shows the history grouped by day with versions, action, result (icon + text)
      and time; "Clear history" asks first.
- [ ] DEBUG demo mode shows a fake history without touching the real file.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
