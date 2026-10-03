# 36 - Printer fixes (branch `feat/printer-fixes`)

"My printer won't print" is one of the most common calls to a family helper. Usually the printer
is set offline, the queue has a stuck job, or the print service has hung. This milestone adds a
"Printers" page that shows each printer's state in plain words and fixes those three things.

## Goals

- New page "Printers" (category `Hardware`, `Order` 3, glyph Segoe Fluent Icons `Print`), headline
  "My printer won't print".
- List of printers read from WMI `Win32_Printer` (`Name`, `Default`, `WorkOffline`, `PrinterStatus`,
  `DetectedErrorState`, `ExtendedPrinterStatus`, `PortName`, `Network`) plus the number of jobs
  waiting per printer from `Win32_PrintJob`.
- Virtual printers ("Microsoft Print to PDF", "Microsoft XPS Document Writer", OneNote, Fax, and any
  printer on a `PORTPROMPT:`/`nul:`/`FILE:` port) go in a collapsed "Other printers" group.
- Each row: name, "Default printer" tag, status as icon + text (Ready / Printing / Offline / Out of
  paper / Paper jam / Paused / Error), jobs waiting in words, network or direct connection.
- Per printer: "Make default", "Clear stuck print jobs" (confirm first), "Print a test page", and for
  an offline printer an explanation plus "Use printer online".
- Page level: "Fix my printer" (guided), "Restart the print service", "Open printer settings"
  (`ms-settings:printers`), "Refresh". Refresh on every navigation to the page.
- Restarting the print service needs administrator rights: the button is disabled when not elevated
  and the shared `AdminRequiredBanner` explains why.
- DEBUG demo mode with a fake printer list and fake actions.

## Non-goals

- Installing, removing or renaming printers, drivers, ports, queue sharing, per-job cancel/pause,
  scanner/fax features.

## Design

### Status decoding (pure, `PrinterStatusDecoder`)

One plain state from three WMI codes. Priority: paper jam (`DetectedErrorState` 8), out of paper (4),
offline (`WorkOffline`, `DetectedErrorState` 9, `PrinterStatus` 7, `ExtendedPrinterStatus` 7/11),
paused (extended 8), error (extended 9, `PrinterStatus` 6, door open / service requested / output bin
full), printing (4 or extended 17), else ready. Low paper and low toner are still "Ready".

### Safety rules

- Every change goes through `IPrinterActions`; ViewModels and `PrinterFixFlow` never touch WMI or the
  service control manager. Tests use fakes: a real queue or the real spooler is never touched.
- Clearing jobs cancels only the named printer's jobs, after a confirmation. Restarting the service
  and the guided fix also ask first.
- Spooler restart: stop (via the shared `IServiceManager`, 30 s timeout constant), optionally delete
  only `*.SPL`/`*.SHD` in `%WINDIR%\System32\spool\PRINTERS` (`SpoolFileFilter`) when jobs were stuck,
  then start again - the service is always started again even if deleting fails.
- Access denied maps to `NeedsAdmin`; nothing is attempted when the user declines a confirmation.

### Guided "Fix my printer" (`PrinterFixFlow`)

1. Check the print service is running; if stopped, start it. If it can't be started, the next two
   steps are reported as skipped.
2. Cancel the default printer's waiting jobs (skipped when there is no default printer).
3. Restart the print service; leftover job files are removed when jobs were found or couldn't be cleared.

Each step reports `Fine` / `Fixed` / `Skipped` / `Failed` / `NeedsAdmin` with a plain sentence, shown
as icon + text in the page, followed by a one-line summary.

### Core (`Porchlight.Core.Printing`)

`PrinterStatusDecoder`, `PrinterClassifier` (virtual detection, job-name parsing), `SpoolFileFilter`,
`IPrinterSource`/`WmiPrinterSource` (reuses `Health.WmiReader`), `IPrinterService`/`PrinterService`,
`IPrinterActions`/`PrinterActions`, `PrinterFixFlow`, `AddPrintersCore()` (real registrations outside the
DEBUG demo branch; `Demo.DemoPrinters` fake inside it).

### App (`Porchlight.App/Features/Printers`)

`PrintersViewModel`, `PrinterRowViewModel`, `FixStepRowViewModel`, `PrintersView`, `PrintersFeature`.
"Use printer online" clears `WorkOffline`; if Windows refuses (`NotSupported`) the page opens
`ms-settings:printers` and explains how to turn off "Use printer offline".

## Tests

`PrinterStatusDecoderTests` (every code path and priority), `PrinterClassifierTests` (virtual printers,
job-name parsing, spool-file filter), `PrinterServiceTests` (default first, virtual flag, failure),
`PrinterFixFlowTests` (state machine with fakes: healthy, stopped spooler, start fails, stuck jobs,
no default printer, clear failure, admin needed, timeout, progress order, cancellation),
`PrintersViewModelTests`, and `AppCompositionTests` (real container validates the new registrations).

## Acceptance criteria

- [ ] The page lists printers with plain-words status (icon + text), default marked, jobs waiting.
- [ ] Virtual printers are in a collapsed "Other printers" group.
- [ ] Clear stuck jobs, Make default, Test page and Use online work; clearing asks first.
- [ ] Restart the print service works when elevated; not elevated shows the admin banner.
- [ ] "Fix my printer" reports each step's outcome in plain words.
- [ ] DEBUG demo mode works; nothing real is changed in tests.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
