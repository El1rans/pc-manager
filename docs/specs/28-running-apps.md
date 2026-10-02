# 28 - Running apps (branch `feat/running-apps`)

"My PC is slow - what is using it?" is the most common question Porchlight can't answer yet. This
milestone adds a "Running apps" page: what is running right now, how much CPU and memory each one
uses, and a safe way to close a stuck app or find where it lives on disk.

## Goals

- New page "Running apps" (category `Apps & services`, `Order` 2, glyph `` Processing / or
  `` TaskManager-like; pick an existing Segoe Fluent glyph).
- Processes are grouped by executable like Task Manager's "Apps" view: 18 `chrome.exe` processes
  are one row "Google Chrome (18)" with summed CPU and memory. Friendly name = the exe's
  `FileDescription` → `ProductName` → file name (reuse `Porchlight.Core.Startup.IFileProductInfoReader`).
- Three sections: "Apps" (a group with at least one visible main window), "Background" (others not
  from Windows), "Windows" (exe inside the Windows folder, or no readable path and session 0).
  "Windows" is collapsed by default.
- Columns: name, CPU % (one decimal), memory (MB/GB, private working set or working set), and for
  groups the process count. Sort by CPU (default), memory or name. A filter box.
- Refreshes every 2 seconds while the page is visible; stops when the user navigates away (the
  `OnNavigatedToAsync` cancellation token is cancelled on navigation - loop until it is).
- Summary line: "CPU 23% · Memory 9.4 GB of 16 GB in use".
- Actions per row: "End task" (with confirmation: "End Google Chrome? Unsaved work in it will be
  lost.") and "Open file location" (opens Explorer with the exe selected).
- DEBUG demo mode with a fake, slowly changing process list.

## Non-goals

- Killing Windows processes, services or Porchlight itself. Suspend, priority, affinity, per-process
  disk/network/GPU, process tree view, command lines, startup history.

## Design

### Safety rules

- "End task" is disabled (with the text "Windows needs this") for the "Windows" section, and the
  service refuses, independent of the UI, any process that is: in the Windows folder, a known
  critical name (`System`, `Idle`, `smss`, `csrss`, `wininit`, `winlogon`, `services`, `lsass`,
  `svchost`, `dwm`, `fontdrvhost`, `Registry`, `MemCompression`, `explorer`), session 0, or the
  current Porchlight process.
- Only pids from the latest snapshot can be ended, and only if the live process still has the same
  start time as in the snapshot (guards against pid reuse). Kill with
  `Process.Kill(entireProcessTree: false)` per pid in the group.
- Access denied → result `NeedsAdmin` ("Porchlight needs administrator rights to end this app");
  already exited → treated as success.
- "Open file location" only ever runs `explorer.exe /select,"<path>"` through `IProcessRunner.StartDetached`,
  and only for a path that exists.

### Core (`Porchlight.Core.RunningApps`)

- `ProcessSample` record: `Pid`, `Name`, `ExecutablePath?`, `SessionId`, `HasMainWindow`,
  `StartTime?`, `TotalProcessorTime`, `MemoryBytes`.
- `IProcessSnapshotSource` / `ProcessSnapshotSource` - wraps `Process.GetProcesses()`. Path via
  `QueryFullProcessImageName` with `PROCESS_QUERY_LIMITED_INFORMATION` (P/Invoke through
  `[LibraryImport]`), NOT `Process.MainModule` (slow, throws for elevated/64-bit mismatches). Every
  per-process read is best effort (logged at Debug once per pid, field falls back). Disposes every
  `Process` object.
- `CpuUsageCalculator` (pure): given the previous and current samples and the elapsed wall time,
  returns per-pid CPU % normalised by `Environment.ProcessorCount`, clamped 0..100; a pid that is new
  or whose start time changed gets 0 for that tick.
- `ProcessGrouper` (pure): groups samples by executable path (case-insensitive; falls back to name),
  sums CPU/memory, decides `RunningAppSection` and `CanEnd` from the safety rules above.
- `ISystemMemoryInfo` - total and available physical memory (`GlobalMemoryStatusEx`); reuse the
  monitoring feature's source if one already exists in `Porchlight.Core.Monitoring`.
- `IRunningAppsService` / `RunningAppsService`: `Task<RunningAppsSnapshot> SampleAsync(ct)` (off the
  UI thread, keeps the previous sample internally) and
  `Task<EndTaskResult> EndAsync(string groupKey, ct)` (`Ended`, `NotFound`, `Refused`,
  `NeedsAdmin`, `Failed`). Uses `IFileProductInfoReader` with a per-path cache.
- `AddRunningAppsCore()` DI extension (fake service in DEBUG demo mode).

### App (`Porchlight.App/Features/RunningApps`)

- `RunningAppsViewModel`: sections of `RunningAppViewModel` rows updated **in place** by group key
  each tick (no list rebuild - keeps scroll position, selection and keyboard focus), sort + filter,
  summary text, end-task confirmation via the app's existing confirmation pattern, friendly result
  line.
- View: scrolling list, works at 900x600, `AutomationProperties.Name` on rows and buttons.

## Tests

`CpuUsageCalculatorTests` (normalisation, new pid, pid reuse, clamping); `ProcessGrouperTests`
(grouping by path, sections, critical names/Windows folder/session 0/self are not endable);
`RunningAppsServiceTests` with a fake snapshot source and fake killer (refuses unknown group,
refuses protected, start-time mismatch skipped, access denied → NeedsAdmin, exited → success);
`RunningAppsViewModelTests` (in-place update keeps row instances, sort, filter, polling stops on
cancellation, confirmation cancel does nothing).

## Acceptance criteria

- [ ] The page lists running apps grouped by executable with CPU %, memory and count, in Apps /
      Background / Windows sections, refreshing every 2 s only while visible.
- [ ] Rows update in place; scroll position and focus are kept.
- [ ] "End task" asks first, never ends Windows/critical processes or Porchlight, and guards pid reuse.
- [ ] "Open file location" opens Explorer with the file selected.
- [ ] DEBUG demo mode works.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
