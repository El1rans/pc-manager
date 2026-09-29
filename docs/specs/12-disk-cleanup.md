# 12 - Free up space (branch `feat/disk-cleanup`)

A new "Free up space" page that (1) cleans safe, regenerable junk (temp files, caches, crash dumps,
Windows Update leftovers, optionally the Recycle Bin) in one click, and (2) *suggests* - never
auto-deletes - big personal files and large installed apps the user might want to remove. Closes
the README's "Cleanup and storage" planned module (the duplicate finder and a folder-size map stay
planned).

The person using this is typically a non-technical parent/grandparent. Every design choice below
favours "can't lose anything that matters" over "freed the most bytes".

## Goals

- One "Scan" that measures every cleanup category off the UI thread, then shows each one as a
  ticked/unticked row with its size, a plain description, and a "needs administrator" note where
  relevant. One "Clean up" button deletes the ticked categories with progress, then reports how
  much space was freed and how many files were skipped (in use / access denied) - skipped files are
  normal and never an error.
- A "Big files" list: the largest personal files (default >= 500 MB) under the user's own folders,
  with "Show in folder" and "Move to Recycle Bin" (after a confirmation) - recoverable by design.
- An "Old downloads" hint: installers and archives in Downloads older than 30 days, same actions.
- A "Large apps" list: installed programs sorted by size, with install date, and an "Uninstall"
  button that runs the program's own uninstaller (after a confirmation).
- A space-freed total remembered in settings ("Porchlight has freed 12.4 GB so far").

## Non-goals (this milestone)

- No scheduled / automatic cleanup. No duplicate finder, no folder-size treemap.
- No registry "cleaning", no driver-store cleanup, no `DISM /ResetBase`, no `Windows.old` removal
  (irreversible; left to Windows' own Storage settings, which the page links to).
- Never deletes anything in the user's personal folders directly - personal files only ever go to
  the Recycle Bin, and only on an explicit per-file click.
- No browser cookies, history, passwords, sessions or profiles - caches only.

## Design

### Core (`Porchlight.Core.Cleanup`)

- **`CleanupCategoryId`** (enum) and **`CleanupCategory`** (record: id, title, description,
  `RequiresAdmin`, `IsSelectedByDefault`, root paths, optional file-name pattern, `MinimumAge`).
  **`CleanupCatalog`** builds the v1 list from an `ICleanupPathProvider` (resolves known folders /
  env vars, so tests can point categories at a temp directory):

  | Category | Paths | Admin | Default | Min age |
  |---|---|---|---|---|
  | Temporary files | `%TEMP%` | no | on | 24 h |
  | Windows temporary files | `%WINDIR%\Temp` | yes | on | 24 h |
  | Browser caches | Edge/Chrome/Brave `User Data\*\{Cache,Code Cache,GPUCache}`, Firefox `Profiles\*\cache2` (under `%LOCALAPPDATA%`) | no | on | none |
  | Thumbnail cache | `%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db` | no | off | none |
  | Crash reports and dumps | `%LOCALAPPDATA%\CrashDumps`, `%LOCALAPPDATA%\Microsoft\Windows\WER`, `%PROGRAMDATA%\Microsoft\Windows\WER` (admin part) | partial | on | none |
  | Windows Update leftovers | `%WINDIR%\SoftwareDistribution\Download` | yes | on | 7 days |
  | Delivery Optimization cache | `%WINDIR%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache` | yes | off | none |
  | Recycle Bin | shell API | no | **off** | n/a |

  An admin-only category shown while not elevated is listed, unticked and disabled, with
  "Restart as administrator to include this" - the page uses the shared `AdminRequiredBanner`
  pattern for the explanation, not an error.

- **`ICleanupFileSystem`** - the only disk access the scanner/cleaner use: enumerate entries
  (name, size, last-write time, attributes), delete a file, delete an empty directory. Real
  implementation `CleanupFileSystem`; tests use an in-memory fake.

- **`CleanupScanner.ScanAsync(categories, progress, ct)`** -> `CleanupScanResult` (per-category
  bytes + file count). **`CleanupRunner.CleanAsync(categories, progress, ct)`** ->
  `CleanupRunResult` (bytes freed, files deleted, files skipped). Both walk the tree iteratively
  (explicit stack, no recursion depth limit issues), run entirely on a background thread, honour
  cancellation between files, and report progress at most every 100 ms.

- **Safety rules (unit tested - these are the point of the feature):**
  1. **Never follow reparse points** (symlinks, junctions, mount points). A reparse-point
     directory is neither entered nor counted; it is never deleted either. This matters because
     `%TEMP%` is user-writable and Porchlight may run elevated - a junction planted in `%TEMP%`
     must not redirect an elevated delete to `C:\Windows` or anywhere else.
  2. Every path about to be deleted is re-checked to be **inside its category root** after full
     path normalisation (`Path.GetFullPath` + ordinal-ignore-case prefix with a trailing
     separator), otherwise skipped and logged at Warning.
  3. A category root that doesn't exist is size 0, not an error. A root that resolves to a drive
     root, `%WINDIR%`, `%USERPROFILE%`, Program Files, or an empty/relative path is rejected by
     `CleanupCatalog` (defensive against a broken env var).
  4. Files newer than the category's `MinimumAge` are left alone (an app may be using them).
  5. A delete that fails (in use, access denied, read-only system file) is counted as skipped and
     logged at Debug - never retried, never forced, never unlocks/kills anything.
  6. Directories are removed only after they are empty, bottom-up; the category root itself is
     never removed.
  7. Browser caches: only the named cache sub-folders, and the category is skipped (reported as
     "Close <browser> to clean its cache") while that browser is running - checked via the
     existing `IProcessProbe`.

- **Recycle Bin**: `IRecycleBin` - `QuerySize()` (`SHQueryRecycleBinW`) and `Empty()`
  (`SHEmptyRecycleBinW` with `SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND`). Off by
  default; its description says plainly "Permanently deletes everything in the Recycle Bin".

- **Big files**: `LargeFileFinder.FindAsync(options, ct)` scans the user's Desktop, Documents,
  Downloads, Videos, Pictures and Music (known-folder paths, deduplicated - e.g. OneDrive-redirected
  Documents), skipping reparse points, hidden/system files, cloud-only placeholders
  (`FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` / `OFFLINE` - reading their size would not free local
  space), returns the top N (default 50) files >= threshold (default 500 MB, setting), largest
  first. **Old downloads**: same finder, Downloads only, extensions `.exe .msi .zip .7z .rar .iso`
  and last-write older than 30 days, any size >= 10 MB.

- **`IRecycler.MoveToRecycleBin(path)`** - `SHFileOperationW` with `FO_DELETE | FOF_ALLOWUNDO |
  FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI`. Returns success/failure; the file is
  recoverable from the Recycle Bin. The only way the page removes a personal file.

- **Installed apps**: `IInstalledAppsReader` enumerates the three uninstall hives (HKLM 64, HKLM
  WOW6432Node, HKCU) -> `InstalledApp` (display name, publisher, version, `EstimatedSize` KB ->
  bytes, `InstallDate` yyyyMMdd parsed, `UninstallString`, `IsPerMachine`). Excludes entries with
  `SystemComponent=1`, a `ParentKeyName` (updates/patches), `ReleaseType` of Update/Hotfix/Security
  Update, no `DisplayName`, or no `UninstallString`; excludes Porchlight itself and the components
  Porchlight depends on (AnyDesk, OpenRGB, PawnIO - removing them from here would silently break
  a page; they have their own install cards). Deduplicates by display name + version. Pure
  filtering/parsing lives in `InstalledAppFilter` / `InstallDateParser` (unit tested).

- **Uninstall**: `IAppUninstaller.StartUninstall(InstalledApp)` splits `UninstallString` with
  `CommandLineToArgvW` (tested parser wrapper for the common `"C:\x\unins000.exe" /arg` and
  `MsiExec.exe /X{GUID}` forms) and launches it via `IProcessRunner.StartDetached` - the app's own
  uninstaller UI runs; Porchlight never uses `QuietUninstallString` or adds silent flags.
  **Elevation rule** (same as `ComponentService`'s `UninstallEntry.IsPerMachine`): while Porchlight
  is elevated, an HKCU (per-user, user-writable) entry is not launched - the button is replaced by
  "Open Installed apps" (`ms-settings:appsfeatures`), so a user-writable registry value can never
  be run with admin rights.

- **DI**: `services.AddCleanupCore()` in `CleanupServiceCollectionExtensions`. DEBUG demo mode
  (`PORCHLIGHT_DEMO_DATA=1`) swaps in fake scanner/finder/apps reader with made-up data, like the
  Dashboard's demo services.

- **Settings**: `CleanupSettings` on `AppSettings` - `LargeFileThresholdMb` (500),
  `DeselectedCategories` (the user's own untick choices, remembered), `TotalBytesFreed`,
  `LastCleanedUtc`.

### App (`Porchlight.App.Features.Cleanup`)

- `CleanupFeature.AddCleanupFeature()` -> `AddCleanupCore()` + `AddPage<CleanupViewModel,
  CleanupView>()`; one line added to `App.xaml.cs`. Title "Free up space", glyph `\uE74D`
  (Delete), `Order = 4`. Nav badge: none.
- Page layout (one scrolling column, works at 900x600):
  1. **Header card**: system drive free/total with a bar (reuse `IDriveMonitor`), the running
     "freed so far" total, a link "Open Windows Storage settings" (`ms-settings:storagesense`).
  2. **Clean up card**: "Scan" -> category rows (checkbox, title, description, size, admin/closed
     browser note) -> total selected -> "Clean up" (disabled while nothing is ticked or while
     running) with a progress bar and "Stop". A confirmation dialog appears only if Recycle Bin is
     ticked. Result line: "Freed 3.2 GB. 41 files were in use and were left alone."
  3. **Big files card** and **Old downloads card**: file name, folder, size, last modified, "Show
     in folder" (`explorer.exe /select,"<path>"` via `IProcessRunner`), "Move to Recycle Bin" with a
     confirmation naming the file and its size. The row disappears on success.
  4. **Large apps card**: name, publisher, size (or "Size unknown"), "Installed 3 years ago",
     "Uninstall..." with a confirmation. Top 25 by size, with "Show all".
- Scanning starts on first navigation (not at app startup), runs once per visit, "Scan again"
  refreshes. Navigating away cancels an in-flight scan but **not** an in-flight clean (a clean is
  short and stopping halfway is harmless but confusing); "Stop" cancels a clean between files.
- The page reports running work through `IBusyGuard` so closing the app mid-clean asks first,
  like Updates does.

## Acceptance criteria

- [ ] Scan shows every category with a size; admin-only categories are disabled with an explanation
      when not elevated; Recycle Bin is unticked by default.
- [ ] Clean up deletes only ticked categories, only files older than the category's minimum age,
      skips in-use files without an error, and reports bytes freed + files skipped.
- [ ] Unit tests prove: reparse points are never followed or deleted; a path outside the category
      root is never deleted; category roots are never deleted; rejected dangerous roots; min-age
      filter; empty-dir bottom-up removal; cancellation stops between files.
- [ ] Big files / old downloads lists exclude cloud-only placeholders, hidden/system files and
      reparse points; "Move to Recycle Bin" is recoverable and asks first.
- [ ] Installed apps list excludes system components, updates, Porchlight and its components;
      unit-tested `UninstallString` parsing; per-user entries are never launched while elevated.
- [ ] Everything runs off the UI thread; the page stays responsive during a scan of a large
      profile.
- [ ] README "Features" updated; "Cleanup and storage" planned module trimmed to what's left.
- [ ] `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes.
