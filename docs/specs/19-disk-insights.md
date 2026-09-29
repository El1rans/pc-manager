# 19 - Disk insights (branch `feat/disk-insights`)

Builds on the Free up space page (`docs/specs/12-disk-cleanup.md`). Adds three things that help a
non-technical user answer "where did my disk space go?" and act on it safely: a disk space map, a
duplicate file finder, and a Dashboard shortcut to the page when a drive is low on space.

Safety and plain language beat power-user features. Everything here is read-only except moving
personal files to the Recycle Bin (recoverable), always after an explicit confirmation.

## Goals

1. **Disk space map** - "What's using space?" card on the Free up space page.
2. **Duplicate file finder** - "Duplicate files" card on the same page.
3. **Dashboard link** - a "Free up space" button on a low-space drive row.

## Non-goals

- No treemap. A sorted list with proportional bars is clearer for this audience.
- No automatic or scheduled deletion; no permanent deletes of personal files, ever.
- No near-duplicate / similar-image detection; only byte-identical files.
- No cross-drive duplicate scan (personal folders only); no hard-link awareness.
- No admin elevation; folders that cannot be read are counted and reported, not forced.

## 1. Disk space map

### Core (`Porchlight.Core.Cleanup`)

- **`IDiskSpaceMapper.MapAsync(root, progress, ct)`** -> `DiskNode` (the root folder). Implemented
  by `DiskSpaceMapper` on top of `ICleanupFileSystem` (same abstraction, so tests use the in-memory
  fake). Runs on a background thread.
- **`DiskNode`**: path, name, parent, total bytes (files below, recursively), file count, the child
  folders (largest first), the largest files directly inside (top 25 per folder, largest first),
  and `UnreadableFolders` (how many folders in this subtree could not be read). Only folders are
  kept in memory, not every file, so a whole drive is affordable.
- **`DiskMapProgress`**: folder being read, bytes and files counted so far. Throttled to one
  update per 100 ms with the shared `ProgressThrottle`.
- **Walk rules** (unit tested):
  1. Iterative (explicit stack, then a reverse-order aggregation pass) - no recursion.
  2. **Never follow a reparse point**; a reparse-point directory is neither entered nor counted,
     and a reparse-point file is not counted.
  3. A folder that cannot be read (`UnauthorizedAccessException` / `IOException`) is skipped, logged
     at Debug, and counted in `UnreadableFolders` ("couldn't read"). Its parent is still totalled.
  4. Cloud-only placeholders (`OFFLINE`, `RECALL_ON_OPEN`, `RECALL_ON_DATA_ACCESS`) are counted as
     0 bytes: they use no local space.
  5. Hidden and system files DO count toward sizes (they take real space) but are never offered
     for the Recycle Bin.
  6. Cancellation is honoured between folders (`OperationCanceledException`).
  7. A root that does not exist throws `DirectoryNotFoundException` (the UI shows a plain message).
- **Which files may be recycled**: a file is flagged `CanRecycle` only if it is inside one of the
  user's personal folders (`ICleanupPathProvider.PersonalFolders`) and is not hidden/system. Nothing
  else on the map has a Recycle Bin button - only "Show in folder".

### App

- Card "What's using space?" on `CleanupView`, driven by `DiskMapViewModel` (a child of
  `CleanupViewModel`, so the page VM stays small).
- **Location picker**: "Your files" (user profile, default), each fixed drive from `IDriveMonitor`
  ("Windows (C:)"), and "Choose a folder..." (`IFolderPicker`, a testable wrapper over the
  standard folder dialog). "Scan" starts; "Stop" cancels. Nothing scans until the user clicks.
- Progress: indeterminate bar + "Counted 12.4 GB in 48,210 files..." text.
- **Drill-down list**: the biggest child folders and files of the current folder, largest first
  (top 30, then one "Everything else" line so the rows add up), each with name, an icon (folder or
  file; not colour-only), a proportional bar (share of the current folder), size and %. Clicking a
  folder ("Open") drills in; a **breadcrumb** ("Your files > Videos > Holidays") goes back up.
  "Show in folder" on every row (`explorer.exe /select,`); "Move to Recycle Bin" (with the existing
  confirmation dialog) only on `CanRecycle` files - the row disappears and the sizes above it are
  reduced on success (the tree is adjusted, no rescan).
- After a scan: "Couldn't read 12 folders (Windows protects them). Their size is not included."
- Screen-reader friendly: each row has an `AutomationProperties.Name` such as
  "Videos, 12.4 GB, 38 percent".

## 2. Duplicate file finder

### Core

- **`IDuplicateFinder.FindAsync(options, progress, ct)`** -> `IReadOnlyList<DuplicateGroup>`,
  `DuplicateFinder`. `DuplicateSearchOptions(Roots, MinimumBytes = 1 MB, MaxGroups = 200)`; the UI
  passes `ICleanupPathProvider.PersonalFolders` (the same set as `LargeFileFinder`, de-duplicated
  with `LargeFileFinder.DistinctRoots`).
- **Candidate files**: regular files >= 1 MB; skips reparse points, hidden/system files/folders and
  cloud-only placeholders (shared rule with `LargeFileFinder`).
- **Stages** (each only sees survivors of the previous one, so the disk is read as little as possible):
  1. Group by exact size; drop groups of one. No file content is read.
  2. Group by a **partial hash**: SHA-256 of the first 64 KB + last 64 KB (the whole file if it is
     no longer than 128 KB); drop groups of one.
  3. Group by a **full SHA-256**, streamed in 1 MB chunks (never loads a file into memory); drop
     groups of one. Only these are reported as duplicates.
- A file that cannot be read (in use, access denied) is skipped and logged at Debug, never fails
  the scan. Cancellation is checked between files and between chunks.
- `IFileContentReader.OpenRead(path)` is the only content access (real implementation + in-memory
  fake in tests).
- **Progress**: stage, files done / total in the stage, throttled to 100 ms.
- **`DuplicateGroup`**: file size, the copies (`DuplicateFile`: path, name, folder, last modified),
  `WastedBytes = size * (copies - 1)`. Groups sorted by wasted space, largest first.
- **`DuplicateSelection.SuggestKeepNewest(group)`** -> the paths to remove: every copy except the
  newest (ties broken by shortest, then alphabetical, path, so the result is deterministic).
- **`IDuplicateRemover.RemoveAsync(groups, selectedPaths, ct)`** moves the chosen copies to the
  Recycle Bin via `IRecycler`. **Always at least one copy per group remains - enforced here, not in
  the UI:**
  1. Only paths that are copies inside a group passed in are ever touched; any other path is ignored.
  2. If the selection covers every copy of a group, the newest is spared (reported as "kept one").
  3. Before touching a group, its surviving copy (an unselected one) is re-checked on disk (still
     exists, same size and modified time). If none can be confirmed, the whole group is skipped.
  4. Each copy to remove is re-checked too (same size and modified time as scanned); a file that
     changed since the scan is skipped ("changed since the scan").
  5. A copy the recycler refuses is counted as failed and never retried or deleted another way.
  6. Cancellation stops between files.
  Result: `DuplicateRemoveResult(FilesRemoved, BytesFreed, FilesFailed, FilesChanged, GroupsKeptOne)`.

### App

- Card "Duplicate files" (`DuplicatesViewModel`): "Find duplicates" / "Stop", stage text ("Comparing
  file sizes...", "Checking file contents...", "Confirming exact matches...").
- Results: one group per block: "3 copies of Holiday.mp4 - 1.2 GB could be freed", listing each copy
  with a checkbox, folder, modified date and a "Newest" tag. Nothing is ticked initially.
  "Keep newest" per group and "Keep newest in every group" tick all but the newest.
  Ticking the last unticked copy of a group is refused in the UI with a plain message (Core
  enforces it regardless). "Show in folder" per copy.
- "Move N files to the Recycle Bin" (disabled with nothing ticked) -> confirmation naming the
  count and size, "You can get them back from the Recycle Bin until you empty it." -> result
  line ("Moved 4 files (2.3 GB) to the Recycle Bin. 1 file was left alone.").
- The page reports an in-flight removal through `IBusyGuard`.

## 3. Dashboard link

- The Dashboard's drive row shows a "Free up space" button when `IsLow`, bound to a command on
  `DashboardViewModel` that calls `IPageNavigator`.
- **`IPageNavigator`** (`Porchlight.App.Shell`): `NavigateTo<TPage>()`; `MainViewModel` handles the
  request by selecting the matching page (UI thread, from a button click). This is the shell's
  first programmatic navigation API; features stay decoupled from `MainViewModel`.

## DI and demo mode

- `AddCleanupCore()` registers `IDiskSpaceMapper`, `IFileContentReader`, `IDuplicateFinder`,
  `IDuplicateRemover`. `CleanupFeature` registers `IFolderPicker`, `DiskMapViewModel` and
  `DuplicatesViewModel`; `CleanupViewModel` takes the last two.
- DEBUG demo mode (`PORCHLIGHT_DEMO_DATA=1`) swaps in fake mapper/finder/remover with made-up data.

## Tests

Core (`tests/Porchlight.Core.Tests/Cleanup/`, in-memory fakes, never the real disk): folder-size
aggregation, reparse points never followed or counted, unreadable folders counted, cloud
placeholders count as zero, top-N files, cancellation, recyclable flag; duplicate stages (size
grouping reads no content, partial hash, full hash only for stage-2 survivors), skip rules, min
size, cancellation, wasted-space maths; keep-newest; the "always keep one copy" rules 1-6.
App: view model tests for drill-down/breadcrumb, recycle flow, duplicate selection guard, and the
Dashboard button.

## Acceptance criteria

- [ ] Map scans "Your files" by default, off the UI thread, cancellable, with progress; never follows
      reparse points; unreadable folders are reported as "couldn't read".
- [ ] Drill-down list with proportional bars, size and %, breadcrumb, "Show in folder"; Recycle Bin
      only for files inside the user's personal folders, after a confirmation.
- [ ] Duplicate finder groups by size, then partial hash, then full SHA-256, streaming and
      cancellable; skips placeholders, hidden/system files and reparse points; shows wasted space
      and a "Keep newest" suggestion; at least one copy per group always remains (Core tests).
- [ ] A low-space Dashboard drive row shows "Free up space", which opens the page.
- [ ] README "Features" updated; "Disk space map" and "Duplicate file finder" removed from planned
      modules.
- [ ] `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes.
