# 13 - Startup apps (branch `feat/startup-apps`)

Many PCs get slow to start because dozens of programs launch themselves at sign-in. This milestone
adds a "Startup apps" page (nav position 5) that lists everything that starts when the user signs
in to Windows, explains each item in plain language, and lets the user turn items off and back on
again - the same way Windows' own Task Manager does it, so nothing is ever deleted.

## Goals

- List every item that starts at sign-in from these places:
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (this user)
  - `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` (all users, 64-bit)
  - `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run` (all users, 32-bit)
  - the per-user Startup folder and the all-users Startup folder (`.lnk` shortcuts; the shortcut's
    target is resolved, the shortcut's file name is the display fallback).
- For each item show a friendly name (the program's own `FileDescription`, then `ProductName`, then
  the registry value / shortcut name), the publisher (`CompanyName`), an On/Off state, where it
  comes from in plain words ("Your account", "All users"), and a short "What is this?" line.
- Mark Microsoft/Windows items and Porchlight's own components (AnyDesk, OpenRGB, Porchlight
  itself) with a "Recommended to keep" note. It is advice only; the user can still turn them off.
- Turn an item off / on **exactly like Task Manager**: write the `StartupApproved` binary value
  (byte 0 = `0x02` enabled, `0x03` disabled, followed by a FILETIME of when it was disabled). Never
  delete a `Run` value or a shortcut; every change is reversible from the same page.
- Items that need administrator rights (everything from `HKLM` and the all-users Startup folder)
  are shown but their switch is disabled while Porchlight is not elevated, with the shared
  `AdminRequiredBanner` explaining why.
- A DEBUG-only demo mode (`PORCHLIGHT_DEMO_DATA=1`) with a fake list, for documentation screenshots.

## Non-goals

- **Startup impact.** Windows measures a per-app boot impact (Task Manager's "Startup impact"), but
  it lives in an undocumented, unstable store. File size or a guess from a name is not impact, so
  the page shows no impact rating at all rather than an invented one.
- **Scheduled tasks that run at logon.** `schtasks /query /xml` output is large, slow and its
  actions are often not simple programs; enabling/disabling them is a different, riskier mechanism.
  Omitted for now; the page says nothing about them. A later milestone can add them read-only.
- Other autostart locations (`RunOnce`, `Winlogon\Shell`, services, `Image File Execution Options`,
  browser extensions). Porchlight does not try to be an Autoruns replacement.
- Deleting, editing or launching a startup entry; adding new ones; killing running processes.

## Design

### Safety rules

- **Only touch what was enumerated.** `IStartupService.SetEnabledAsync` takes the id of an entry
  returned by the last `ListAsync` and refuses anything else. The registry is only ever written at
  the `StartupApproved` value whose name equals that entry's `Run` value name / shortcut file name.
- **Never launch a startup command.** Commands are only parsed to find the executable path, and
  that path is only ever passed to `FileVersionInfo.GetVersionInfo` (reads the file's version
  resource). Nothing is executed, opened or shelled out to.
- **Elevation rule.** An entry from a per-machine source (`HKLM`, all-users folder) is only
  changed when `IElevationService.IsElevated`; otherwise `SetEnabledAsync` returns `NeedsAdmin`
  without writing, independent of what the UI shows.
- Reads are best effort: a key that cannot be read, a shortcut that cannot be resolved or an
  executable that cannot be inspected is logged at Debug and that field falls back (the entry is
  still listed).

### Core (`Porchlight.Core.Startup`)

- `StartupSource` - `CurrentUserRun`, `MachineRun`, `MachineRun32`, `CurrentUserFolder`,
  `MachineFolder`; `StartupSourceExtensions` gives `IsPerMachine` and a plain label.
- `StartupApprovedBlob` - pure parse/build of the 12-byte `StartupApproved` value.
  `IsEnabled(byte[]?)`: a missing or empty value means enabled; otherwise the low bit of byte 0
  decides (`0x02`/`0x06` enabled, `0x03`/`0x07` disabled). `CreateEnabled()` = `02` + 11 zero bytes.
  `CreateDisabled(now)` = `03 00 00 00` + `now.ToFileTime()` little-endian.
- `StartupCommandParser.ExtractExecutablePath(command)` - handles a quoted path, an unquoted path
  containing spaces (up to the first `.exe`), a bare token, and environment variables; returns
  `null` for an empty command.
- `StartupClassifier` - pure: from name, path and publisher decides `RecommendedToKeep`
  (Microsoft publisher, a path inside the Windows folder, or Porchlight/AnyDesk/OpenRGB) and picks
  a plain hint from a small table of well-known programs (OneDrive, Teams, Spotify, Discord, Steam,
  Zoom, Dropbox, Adobe, Google, NVIDIA, Realtek...), else a generic sentence built from the file
  description.
- `IStartupRegistry` / `StartupRegistry` - reads a source's `Run` values (string values only) and
  reads/writes one `StartupApproved` value. For a per-machine source the effective state is the
  per-user approval value if present, else the per-machine one; a write goes to the per-machine
  `StartupApproved` key (and updates the per-user value too if one already exists so the two never
  disagree).
- `IStartupFolderReader` / `StartupFolderReader` - lists files of a Startup folder (ignoring
  `desktop.ini`) and resolves each `.lnk` target through `WScript.Shell`.
- `IFileProductInfoReader` / `FileProductInfoReader` - `FileVersionInfo` description/product/company.
- `IStartupService` / `StartupService` - assembles `StartupEntry` records (`ListAsync`, run off the
  UI thread) and performs `SetEnabledAsync(id, enabled)` returning `StartupChangeResult`
  (`Changed`, `NotFound`, `NeedsAdmin`, `Failed`).
- `AddStartupCore()` registers the above (a fake service under `#if DEBUG` demo mode).

### App (`Porchlight.App/Features/Startup`)

- `StartupViewModel` (`Title` "Startup apps", `Order` 5, glyph Power). Refreshes every time the page
  is navigated to and on a "Refresh" button. Shows a summary ("12 apps start with Windows - 9 on,
  3 off"), an empty state, a friendly error line, and the `AdminRequiredBanner` only when the page
  is not elevated and at least one listed item needs it.
- One card row per `StartupEntryViewModel`: name (bold), publisher, source label, the hint, a
  "Recommended to keep" note when applicable, and a status made of an icon plus the words "On" /
  "Off" (never color alone) with a "Turn off" / "Turn on" button. Items that need admin while not
  elevated have the button disabled and the text "Needs administrator".
- Turning an item off tells the user it takes effect the next time they sign in and can be undone
  here. A failure is shown as a plain sentence.
- Full keyboard access; every button has an `AutomationProperties.Name` naming the app.

## Deviations from the brief

- No startup impact and no scheduled tasks (see Non-goals), by design.

## Tests

`StartupApprovedBlobTests` (missing/empty = enabled, `02`/`06`/`03`/`07` decoding, build enabled,
build disabled round-trips the FILETIME); `StartupCommandParserTests` (quoted, unquoted with spaces
and args, env variables, empty); `StartupClassifierTests` (Microsoft, Windows folder, Porchlight
components, known program hints, generic fallback); `StartupServiceTests` against fake registry,
folder and file-info readers (listing from each source, name/publisher fallback chain, disabled
state read, enable/disable writes the correct blob to the correct source, per-machine refused when
not elevated, unknown id refused, read failure tolerated); `StartupViewModelTests` (summary, admin
banner, admin-only entries not changeable, toggling refreshes).

## Acceptance criteria

- [ ] The page lists items from `HKCU`/`HKLM` `Run`, `HKLM` WOW6432Node `Run`, and both Startup
      folders, each with name, publisher, source, On/Off and a hint.
- [ ] Microsoft/Windows and Porchlight-owned items show "Recommended to keep".
- [ ] Turning an item off writes only the matching `StartupApproved` value (`0x03` + FILETIME);
      turning it on writes `0x02`; no `Run` value or shortcut is ever deleted or modified.
- [ ] Per-machine items cannot be changed unless elevated (UI disabled and service refuses); the
      admin banner appears only when relevant.
- [ ] No startup command is ever executed.
- [ ] Status is icon + text, not color alone; the page works from 900x600 and scrolls.
- [ ] DEBUG demo mode shows a fake list without touching the registry.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
