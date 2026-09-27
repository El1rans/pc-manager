# 02 - App updates via winget (branch `feat/updates`)

> **Amended after 01b:** `WingetOutputReader` and the generic process runner (`IProcessRunner`) already exist from milestone 01b (Components). Reuse them; `IWingetClient` builds on `IProcessRunner`. Do not duplicate them.

Goal: port `prototype/WingetUpdater.ps1` into the app as the Updates page, with the same behaviour and better structure. Read the prototype first; its parsing and output-handling logic is proven against real winget output.

## Core (`PCManager.Core/Winget`)

- `WingetOutputReader`: incremental char-stream splitter. `\n` (or `\r\n`) ends a line; a lone `\r` means winget is redrawing, so the pending text is a progress update. Spinner-only frames (`-`, `\`, `|`, `/`) and whitespace are dropped. Handles `\r\n` split across two `Feed` calls.
- `WingetTableParser.Parse(IReadOnlyList<string> lines)`: finds each table by the dashed separator line, takes column start positions from the header line above it, slices each row by those positions. Rows stop at a blank line or a line too short to reach the 4th column (summary lines like "10 upgrades available."). The first table = normal upgrades; any later table (e.g. "require explicit targeting") sets `RequiresExplicit = true`. Returns `WingetPackage(Name, Id, InstalledVersion, AvailableVersion, Source, RequiresExplicit)`.
- `IWingetClient` / `WingetClient`:
  - `GetUpgradesAsync(bool includeUnknown, IProgress<string>? progress, CancellationToken ct)`: `winget upgrade --accept-source-agreements --disable-interactivity [--include-unknown]`.
  - `UpgradeAsync(string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken ct)`: `winget upgrade --id <id> --exact --include-unknown --accept-package-agreements --accept-source-agreements --disable-interactivity [--silent]`. Returns `WingetResult(ExitCode, Lines)`.
  - `ShowAsync(string id, IProgress<string>? log, CancellationToken ct)`: `winget show --id <id> --exact`.
  - Uses `ProcessStartInfo.ArgumentList` (no string concatenation), UTF-8 stdout/stderr, `CreateNoWindow`, reads stderr concurrently. Missing winget -> `WingetNotFoundException` with a helpful message.
  - Cancellation between packages only; never kill a running installer.
- `WingetExitCodes`: named constants and `Describe(int code)` -> `(PackageOutcome, string message)`. Known: `0` success; `0x8A15002B` no applicable upgrade (Skipped); `0x8A150101` app is running, close it (Failed). Unknown -> "Failed (0xXXXXXXXX)". Success whose output mentions a restart -> "Updated - restart needed".

## App (`PCManager.App/Features/Updates`)

Page layout (top to bottom): title + summary line ("10 updates available (1 ignored) - last checked 14:32"), toolbar (Refresh, Select all, Select none, filter box, "Show ignored" toggle on the right), DataGrid, action row (Silent install, Include apps with unknown version, "Stop after current" while running, accent "Update selected (N)"), progress row (indeterminate bar + current step + latest winget progress line), collapsible Log panel (monospace, auto-scroll, capped at 200k chars, "Open log folder", "Clear").

DataGrid columns: checkbox, Name, ID, Installed, Available, Notes ("Ignored" / "Pinned / explicit only" / "Current version unknown"), Status (colored by state + text: Queued, Updating..., Updated, Failed, Skipped). Sortable. Double-click toggles the checkbox. Right-click menu on selected rows: Ignore, Stop ignoring, Copy ID, Show package info (runs `ShowAsync` into the log).

Behaviour:
- Check runs automatically when the app starts (in the background, unless "Check for updates when PC Manager starts" is turned off) and on Refresh. Nav badge shows the count of non-ignored updates (hidden when 0).
- Default selection: all non-ignored, non-explicit packages are ticked.
- Update runs selected packages one by one, updates each row's status live, then re-checks quietly; rows that still exist keep their last status, successfully updated rows disappear. A final summary line: "Finished: 5 updated, 1 failed, 0 skipped". Play `SystemSounds.Asterisk`.
- Ignore list, Silent, Include-unknown, Check-on-startup persist in the `Updates` settings section.
- Everything written to the Log panel also goes to the app log.

## Tests

- Parser: the real sample below parses to 10 packages; `Discord.Discord` has `RequiresExplicit = true`; `Microsoft Visual Studio Code (User)` name kept intact; "Unknown" version kept; summary line and blank lines are ignored; empty/"No installed package found" output returns 0 packages.
- Output reader: `\r\n` split across feeds, spinner frames dropped, progress vs line classification, trailing text without newline flushed on `Complete()`.
- Exit codes: each known code maps correctly; restart detection.

```
Name                                Id                         Version       Available     Source
-------------------------------------------------------------------------------------------------
Battle.net                          Blizzard.BattleNet         Unknown       1.19.3.3219   winget
GOG GALAXY                          GOG.Galaxy                 Unknown       2.1.8.30      winget
Google Cloud SDK                    Google.CloudSDK            Unknown       586.0.0       winget
Microsoft Edge                      Microsoft.Edge             153.0.4234.48 154.0.4258.37 winget
Microsoft GameInput                 Microsoft.GameInput        3.5.274.0     3.5.278       winget
Microsoft Visual Studio Code (User) Microsoft.VisualStudioCode 1.139.0       1.139.1       winget
OBS Studio                          OBSProject.OBSStudio       31.0.3        32.2.2        winget
Oh My Posh version 24.8.0           JanDeDobbeleer.OhMyPosh    24.8.0        31.3.0        winget
WinRAR 6.24 (64-bit)                RARLab.WinRAR              6.24.0        7.23.0        winget
10 upgrades available.

The following packages have an upgrade available, but require explicit targeting for upgrade:
Name    Id              Version  Available Source
-------------------------------------------------
Discord Discord.Discord 1.0.9258 1.0.9259  winget
```

## Acceptance criteria

- [ ] All tests above pass.
- [ ] Page lists real updates on this PC, selection works, filter works, ignore persists across restarts.
- [ ] Updating one small package works end to end, status and log update live, UI never freezes.
- [ ] "Stop after current" marks remaining rows Skipped ("Cancelled").
- [ ] Nav badge shows the update count.
