# 16 - Check-up report and app list export/import (branch `feat/checkup-report`)

Two small features that make remote help easier for a non-technical user and the family member who
maintains their PC ("the helper"):

- **A. Check-up report** - on the "Get help" page, one click builds a plain-language health summary
  of this PC that the user can copy, save, or open in an email to their helper.
- **B. Move to a new PC** - on the Updates page, save the list of installed apps to a file
  (`winget export`) and install the apps from such a file on another PC (`winget import`).

Porchlight never sends email itself and never uploads anything. The user always sees the report
before it leaves the app, and always chooses to send it.

## Goals

- A. A "Send a check-up to <helper name or 'your helper'>" card on the Get help page with
  **Create check-up**, then a read-only preview of exactly what will be shared and three actions:
  **Copy report**, **Save report...**, **Email it**.
- A. An extensible design: any feature can contribute a section by registering an `ICheckupSection`
  in DI; no edit to the report builder or the page is needed.
- A. Sections for what exists today: computer and Windows, uptime and pending restart, drive space,
  app updates pending, temperatures (when the hardware service has them), remote help (AnyDesk).
- A. Output as plain text (clipboard, email body, `.txt`) and as a self-contained HTML file (`.html`).
- B. "Save my app list..." and "Install apps from a list..." under a "Move to a new PC" expander on
  the Updates page, with the same live log/progress the page already uses. Before installing, the
  app list from the file is shown for confirmation.
- B. Export files are parsed and validated in Core (not a winget export -> rejected; size capped).

## Non-goals

- **Weekly reminder** ("Remind me to send a check-up every week"): needs the tray/notifications
  work that lives on another branch. Not built here.
- Sending email from Porchlight (SMTP, MAPI, Graph), uploading the report anywhere, or any helper
  "inbox". The report only leaves the PC if the user copies/saves/emails it themselves.
- Scheduled or automatic reports; report history.
- Restoring anything other than apps (settings, files, licences) on the new PC.
- Choosing individual apps to skip during import (the user can cancel, or edit the file).
- Exporting/importing the app list to/from the cloud.

## Design

### A. Check-up report (`Porchlight.Core/Checkup`)

- `CheckupSeverity`: `Ok`, `NeedsAttention`, `Problem` (ordered; the overall status is the worst).
- `CheckupSectionResult(Title, Severity, Lines)`: a titled list of plain-language lines with one
  severity for the section.
- `ICheckupSection` (**the extension point**): `string Title`, `int Order`,
  `Task<CheckupSectionResult?> BuildAsync(CancellationToken)`. Return `null` to leave the section
  out (for example no temperature sensors). A new feature adds a section with one line:
  `services.AddSingleton<ICheckupSection, MySection>();` - it is picked up automatically, ordered by
  `Order` (built-in sections use 100, 200, 300, ...; leave gaps). A section must be read-only, fast
  (use cached data; never start expensive work such as a winget run) and follow the privacy rules
  below.
- `ICheckupReportBuilder` / `CheckupReportBuilder`: runs every section (a section that throws is
  logged and shown as "Porchlight could not check this" with `NeedsAttention` - one broken section
  never blocks the report) and returns a `CheckupReport(GeneratedAt, ComputerName, Sections,
  OverallSeverity)`.
- `CheckupTextRenderer` and `CheckupHtmlRenderer`: pure functions from a report to text/HTML. HTML
  is one file with inline CSS, no scripts, no external requests, light/dark aware; every status
  shows a text label ("OK", "Needs attention", "Problem"), never colour alone. All text is
  HTML-encoded.
- `CheckupMailto.Build(helperEmail, subject, body)`: builds a `mailto:` URI. The recipient is only
  used if it passes a strict address check (no spaces, one `@`, no URI/header metacharacters).
  Total URI length is capped at 1800 characters (below the ~2000 limit of common Windows mail
  handlers); when the body does not fit it is cut at a line boundary and ends with a note that the
  full report can be saved with "Save report...".
- Built-in sections (`Porchlight.Core.Checkup.Sections`), each behind existing interfaces:

  | Section (order) | Source | Severity |
  |---|---|---|
  | Computer and Windows (100) | `ISystemInfoProvider` (computer name, Windows, make/model, memory) | Ok |
  | Restarts (200) | `SystemInfo.LastBootTimeUtc`, `IRestartDetector` | pending restart or 14+ days up: Needs attention |
  | Drive space (300) | `IDriveMonitor` (drive letter, free/total; label omitted) | `IsLow`: Needs attention; under 5 GB free: Problem |
  | App updates (400) | `IPendingUpdatesTracker` (count and time of the last check; **no** new winget run) | any pending: Needs attention |
  | Temperatures (500) | `IHardwareService.Latest` via `HardwareSummarySelector`; omitted unless `Ready` with a CPU/GPU temperature | within 15 C of the failsafe: Needs attention; at/over: Problem |
  | Remote help (600) | `IAnyDeskService` (installed/running; **not** the address) | not installed or not running: Needs attention |

- `IPendingUpdatesTracker` (`Porchlight.Core/Winget`): singleton holding the last check's
  non-ignored update count and time. `UpdatesViewModel` reports to it after each check. Before any
  check the section says "Porchlight hasn't checked for app updates yet."

**Privacy (hard rules, unit-tested).** The report never contains: the Windows user name, file or
folder names/paths, the list of installed apps (only a count of pending updates), IP/MAC addresses,
serial numbers or hardware IDs, drive labels, or the AnyDesk address. It does contain the computer
name, Windows version, make/model and memory size, because the helper needs them to recognise the
PC. A builder test feeds fake sensitive values through every input and asserts none appear in the
text or HTML output.

**Helper email.** New optional `RemoteSupportSettings.HelperEmail` (default empty), edited from the
check-up card in a text box and saved on focus loss through `ISettingsStore.Update`. With no
(valid) email, "Email it" still opens a new message with the report and no recipient.

### A. App layer (`Porchlight.App/Features/RemoteSupport`)

- `CheckupCardViewModel` (owned by `RemoteSupportViewModel`, exposed as `Checkup`): builds the
  report off the UI thread, exposes the plain text preview, and implements the three actions.
  Copy uses the existing `IClipboardService`; Email opens `CheckupMailto` through the existing
  `IUrlLauncher`; Save uses the new `IFileDialogService` (`Shell/`), default folder Desktop,
  default name `Porchlight check-up yyyy-MM-dd.html`; `.txt` saves plain text, anything else HTML.
  Results ("Copied", "Saved to ...", "Couldn't save the file. Try another folder.") appear in a
  status line with an icon and text. The existing "Copy support info" button is unchanged.

### B. App list export/import

- `IWingetClient.ExportAsync(path, log, progress, ct)` runs
  `winget export -o <path> --accept-source-agreements --disable-interactivity`.
- `IWingetClient.ImportAsync(path, log, progress, ct)` runs
  `winget import -i <path> --accept-package-agreements --accept-source-agreements
  --ignore-unavailable --disable-interactivity`. As with installs, once started winget is never
  killed mid-run (the page passes `CancellationToken.None`).
- `WingetExportParser.Parse(json)` -> `WingetExportParseResult` (success with a list of
  `WingetExportApp(Id)`, or a plain-language failure reason). Rules: valid JSON, root object with a
  `Sources` array whose entries hold `Packages[].PackageIdentifier` strings (the winget export
  schema); ids must be printable with no whitespace, at most 256 characters; invalid ids are
  dropped and counted; at most 2000 apps; zero usable apps is a failure.
  `WingetExportFile.LoadAsync(path)` enforces a 2 MB size cap before reading.
- Updates page: a "Move to a new PC" expander with the two buttons. "Save my app list..." opens a
  save dialog (`.json`). "Install apps from a list..." opens a file dialog, parses the file, and
  shows a confirmation card listing the apps (scrollable) with **Install these apps** / **Cancel**.
  Both run with the page busy state and stream to the existing log and progress line. A finished
  import ends with a plain summary and triggers a quiet re-check.

## Acceptance criteria

A. Check-up
1. The Get help page shows a card titled "Send a check-up to <helper name>" (or "your helper"),
   whether or not AnyDesk is installed.
2. Create check-up builds the report without freezing the window and shows it as a preview.
3. Copy report puts the plain text on the clipboard and says so; a clipboard failure is reported.
4. Save report... opens a save dialog in the Desktop folder; `.html` writes a self-contained HTML
   file, `.txt` plain text; a write failure shows a plain message and is logged.
5. Email it opens the default mail client with the helper email as recipient (if set and valid),
   a subject naming the computer, and the report in the body, under 1800 characters of URI; a
   too-long report is cut with a note about Save report.
6. The report contains none of the values listed under Privacy (unit test with fake sensitive data).
7. A section that throws does not stop the report; a second `ICheckupSection` registered in a test
   appears in the output in `Order`.
8. The text and HTML renderers are covered by tests (severity labels, HTML encoding).

B. Move to a new PC
9. Save my app list... runs the export command with the chosen path and reports success/failure
   in the log and summary.
10. Install apps from a list... rejects a file that is not a winget export or is over the size cap
    with a plain message, and otherwise shows the apps to be installed before doing anything.
11. Cancel does nothing; Install these apps runs the import with live log/progress.
12. Tests never start a real winget process.

General: `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes; README
"Features" updated and "export/import an app list" removed from planned modules.
