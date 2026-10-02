# 30 - Startup impact and logon tasks (branch `feat/startup-impact-and-tasks`)

Spec 13 deliberately left two things out of the Startup apps page: a startup impact rating and
scheduled tasks that run at logon. This milestone adds both, using only data Windows itself records,
so the page can answer "which of these is actually slowing my sign-in?".

## Goals

- **Impact.** Each startup entry shows "High impact", "Medium impact", "Low impact" or
  "Not measured" (icon + text), using Microsoft's documented Task Manager thresholds:
  - High: more than 1 second of CPU time **or** more than 3 MB of disk I/O at startup.
  - Medium: 300 ms - 1 s CPU **or** 300 KB - 3 MB disk.
  - Low: under 300 ms CPU **and** under 300 KB disk.
  A "Sort by impact" option, and the summary gains "2 have high impact".
- **Logon tasks.** Scheduled tasks that have an enabled logon trigger and an executable action are
  listed in the same page as a new source "Scheduled task", with name, publisher, hint and On/Off.
  Tasks in the `\Microsoft\` folder are listed only when they are not Windows' own (i.e. skip the
  whole `\Microsoft\Windows\` tree) and are marked "Recommended to keep".
- Turning a logon task off/on sets the task's `Enabled` flag (reversible, like Task Scheduler) -
  never deletes or edits the task.
- DEBUG demo mode includes impact values and two fake tasks.

## Non-goals

- Running a startup trace or measuring boot time ourselves. Tasks with only COM-handler actions,
  non-logon triggers (time, idle, event), or tasks under `\Microsoft\Windows\`.

## Design

### Impact data source

Task Manager reads `%windir%\System32\wdi\LogFiles\StartupInfo\<user SID>_StartupInfo<N>.xml`
(written by the Diagnostic Policy Service for recent boots). Each file has per-process elements
with the image path / command line and CPU time and disk usage measured during startup.

- `IStartupInfoReader` / `StartupInfoReader`: finds the current user's files (SID via
  `WindowsIdentity.GetCurrent().User`), reads the newest file only, and parses it with
  `StartupInfoParser` (pure, XML → list of `(ImagePath, CpuTimeMs, DiskBytes)`). Inspect a real file
  on the dev machine (if readable) and keep a sanitised copy as a test fixture; be tolerant of
  missing elements/attributes and of unit variations; never throw (log at Debug, return empty).
- The folder normally needs administrator rights. When reading fails with access denied, every entry
  is "Not measured" and the page shows one line: "Run Porchlight as administrator to see startup
  impact." (with the existing restart-as-admin command) - not the full admin banner.
- `StartupImpactRater` (pure): thresholds above as named constants; matches an entry to a record by
  executable path (case-insensitive, environment variables expanded, `\\?\`/`\Device\` prefixes
  normalised); no match → `NotMeasured`.
- `StartupEntry` gains `StartupImpact Impact` (`NotMeasured`, `Low`, `Medium`, `High`).

### Logon tasks

- `StartupSource.LogonTask` (per-user unless the task runs as another principal; `IsPerMachine`
  for tasks whose principal is not the current user or that run with highest privileges).
  Label "Scheduled task".
- `ILogonTaskSource` / `LogonTaskSource` through the Task Scheduler COM API (`Schedule.Service`,
  late-bound `dynamic` like `StartupFolderReader` uses `WScript.Shell`, released with
  `Marshal.FinalReleaseComObject`). Enumerate folders recursively (skipping `\Microsoft\Windows\`),
  include tasks with a `TASK_TRIGGER_LOGON` (type 9) trigger that is enabled, and take the first
  `TASK_ACTION_EXEC` action's path. Read on an STA thread if COM requires it.
- `SetEnabledAsync` for a task id (`task|<task path>`) sets `IRegisteredTask.Enabled`. Same rules as
  spec 13: only ids from the last list; per-machine task when not elevated → `NeedsAdmin`;
  access denied → `NeedsAdmin`.
- Name/publisher/hint come from the action executable via the existing `IFileProductInfoReader`
  and `StartupClassifier`, falling back to the task name.

### App

- `StartupEntryViewModel` shows the impact chip and the "Scheduled task" source; `StartupViewModel`
  gets the sort option, the impact summary part and the not-measured hint line.

## Tests

`StartupInfoParserTests` (fixture file, missing fields, garbage XML → empty);
`StartupImpactRaterTests` (each threshold boundary, either-metric rule, path normalisation, no match);
`StartupServiceTests` additions (impact attached; tasks listed with the right source; task
enable/disable writes only `Enabled`; per-machine task refused when not elevated; access denied on
impact read → NotMeasured + flag); `StartupViewModelTests` additions (sort by impact, summary, hint
line only when access denied).

## Acceptance criteria

- [ ] Each startup item shows High/Medium/Low/Not measured from Windows' own StartupInfo data with
      the documented thresholds; the page can sort by impact.
- [ ] When impact can't be read without admin, one plain line says so with a restart-as-admin action.
- [ ] Logon scheduled tasks (outside `\Microsoft\Windows\`) appear as "Scheduled task" items and can
      be turned off/on via the task's Enabled flag only.
- [ ] No startup command or task is ever run.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
