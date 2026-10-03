# 33 - Backup status (branch `feat/backup-status`)

The Health page tells a person whether the disk is healthy, but not the question that matters
most when something goes wrong: is anything backing up my files, and when did it last run? This
milestone adds a read-only "Backup" card to the Health page (and a matching Check-up report
section) that answers that in plain words.

## Goals

- New "Backup" card on the Health page, loading and failing independently like the other cards
  (`HealthCardViewModelBase`).
- One verdict, shown as icon + text (never color alone):
  - **Good** - a backup ran within `RecentBackupDays` (7) days, or OneDrive protects both
    Documents and Desktop.
  - **Warning** - a backup is set up but is out of date (File History last ran more than
    `RecentBackupDays` days ago, or never), or OneDrive protects only some of the main folders.
  - **Problem** - "Nothing is backing up your files."
- Detail lines: File History (not set up / on, last backup time), OneDrive (not installed / not
  signed in / signed in and which of Desktop, Documents, Pictures it protects), and an
  "Also found: ..." line for well-known backup tools.
- Plain nudge text and buttons: "Turn on File History" (when File History is not on and nothing
  recent covers the user), "Open backup settings" (`ms-settings:backup`), and "Open OneDrive" when
  OneDrive is installed.
- DEBUG demo fake. Included in the Check-up report.

## Non-goals

- Changing any backup configuration, starting a backup, or signing in to anything. The card only
  reads and opens Windows' own settings screens.
- Judging third-party tools: they are listed ("Also found") but never counted as good or bad, and
  Porchlight cannot tell whether they are working.
- Showing the OneDrive account email, folder paths or the File History drive name.

## Design

### Sources (Core, `Porchlight.Core.Backup`)

Each source is behind an interface. The aggregating service catches and logs a failing source and
carries on with the others; only when **both** File History and OneDrive could not be read does the
card show "Couldn't check".

**File History** (`IFileHistoryReader` / `FileHistoryReader`). Used:
- Configuration: `%LOCALAPPDATA%\Microsoft\Windows\FileHistory\Configuration\Config*.xml`. A file
  with a non-empty `TargetUrl` (or `TargetName`) means a backup drive was chosen =
  *configured*. `FileHistoryConfigParser` (pure) reads it tolerantly: an `Enabled`/`UserEnabled`
  element, when present, gives the on/off state; when absent a configured target counts as on.
  No config folder = File History was never set up.
- Last backup: registry `HKCU\Software\Microsoft\Windows\CurrentVersion\FileHistory`, value
  `ProtectedUpToTime` (a QWORD FILETIME that File History's service updates after each successful
  run; it is "protected up to" and so equals the last completed backup). Missing/zero = never.
- Not used: the target drive's `FileHistory` catalog (the drive may be unplugged, and reading it is
  slow). A missing value is treated as "never ran".

**OneDrive** (`IOneDriveReader` / `OneDriveReader`):
- Accounts: `HKCU\Software\Microsoft\OneDrive\Accounts\*` with `UserFolder` and `UserEmail`. An
  account with both non-empty is *signed in*. The email is only tested for presence and never
  stored or shown. Installed = an account key exists or `%LOCALAPPDATA%\Microsoft\OneDrive\OneDrive.exe` exists.
- Known Folder Move: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders`
  values `Desktop`, `Personal` (Documents), `My Pictures`; a folder is protected when its path is
  inside any signed-in account's `UserFolder` (`KnownFolderCoverage`, pure: environment variables
  expanded, case-insensitive, separator-normalised).

**Other tools** (`IBackupToolDetector` / `BackupToolDetector`): reuses `IInstalledAppsReader`
(Cleanup) and the pure `BackupToolMatcher` against a small list (Macrium Reflect, Acronis, Veeam
Agent, Backblaze, Google Drive, Dropbox, iDrive, Carbonite, EaseUS Todo Backup, Windows Backup
apps). The Check-up report never names them (privacy rule: no installed-app names), it only says
another backup program is installed.

### Verdict (`BackupVerdictEvaluator`, pure)

`Evaluate(BackupSnapshot, DateTimeOffset now)` returns a `BackupAssessment` (verdict, headline,
detail lines, which buttons to offer). Constants: `RecentBackupDays = 7`. A source that could not
be read is treated as "unknown" and described as "Couldn't check ..." rather than as absent. When
the verdict is Problem and other tools were found, the text adds that Porchlight cannot tell if
those are working.

### Service and registrations

`IBackupStatusService.GetAsync(ct)` runs the readers off the UI thread and returns
`HealthReadResult<BackupSnapshot>`. `AddBackupCore()` (called from `AddHealthCore`) registers the
real readers outside the DEBUG demo branch and the demo service inside it. `TimeProvider` is
registered with `TryAdd`. The Check-up `BackupCheckupSection` (order 350) maps Good/Warning/Problem
to Ok/NeedsAttention/Problem.

### App

`BackupCardViewModel : HealthCardViewModelBase` with `RefreshAsync`, headline, detail lines,
"Also found" text, severity (`HealthSeverity` gains `Problem`, with an error glyph and the critical
brush) and three launch commands via `IProcessRunner.StartDetached`:
`control.exe /name Microsoft.FileHistory`, `explorer.exe ms-settings:backup`, and the OneDrive exe.
`HealthViewModel` refreshes it with the other cards.

## Tests

`BackupVerdictEvaluatorTests` (every verdict, boundary at the day threshold, never-ran, partial
OneDrive, unknown sources, other tools note), `FileHistoryConfigParserTests`,
`KnownFolderCoverageTests`, `BackupToolMatcherTests`, `BackupStatusServiceTests` (a throwing
source is tolerated; both failing = Fail), `BackupCheckupSectionTests` (severity mapping, no email
or app names), `BackupCardViewModelTests` (state, failure, buttons launch the right processes),
and `AppCompositionTests` covers the new registrations.

## Acceptance criteria

- [ ] The Health page shows a Backup card with Good / Warning / Problem as icon + text.
- [ ] File History last-backup time and OneDrive Documents/Desktop/Pictures coverage are shown;
      the OneDrive email never appears.
- [ ] "Also found: ..." lists known backup tools without judging them.
- [ ] The buttons only open Windows' own screens; nothing about backup config is changed.
- [ ] The Check-up report includes a Backup section without app names or the email.
- [ ] DEBUG demo mode works; `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
