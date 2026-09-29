# 14 - System health (branch `feat/system-health`)

Porchlight already shows live stats (Dashboard), updates apps (Updates) and drives hardware. This
milestone adds a **Health check** page (nav order 6) that answers the questions a family member
maintaining a relative's PC asks most: "Is the disk dying?", "Is Windows itself damaged?", "Can I
undo a change if something goes wrong?", "What has been going wrong lately?" and, on a laptop, "Is
the battery worn out?". Every answer is a plain sentence, never a raw WMI/Event Log value.

## Goals

- One page, five cards: **Disk health**, **Windows repair**, **Restore point**, **Recent problems**
  and (only on a device with a battery) **Battery**.
- Plain-language verdicts with an icon plus a text label (status is never colour alone).
- Pure, unit-tested Core logic for every verdict, parser, grouping and calculation; all WMI /
  Event Log / process access sits behind an interface.
- Every read runs off the UI thread with a timeout. A failed read shows "Couldn't check" with a
  short reason and a "Check again" button - it never crashes the page or the app.
- Safe by default: read-only cards never need admin; the two actions that change something (repair,
  restore point) need admin, say so with the shared `AdminRequiredBanner`, and explain what they do.
- DEBUG-only demo fakes (same gate as `Monitoring/Demo`) so screenshots never show the real PC.

## Non-goals

- No disk repair (`chkdsk`), no SMART attribute table, no firmware/drive-vendor tooling.
- No restore-point deletion, restore, or System Protection configuration - the page only links to
  Windows' own System Protection dialog.
- No cancelling of an SFC/DISM run once it has started, no scheduling, no history of past runs.
- No Event Log clearing or export; no problem categories beyond the five below.
- No localisation of SFC/DISM output parsing (English phrases; other languages fall back to
  "Unknown" with the raw log still available - see Windows repair).

## Design

### Core (`Porchlight.Core.Health`)

Shared: `HealthReadResult<T>` (success value or a short plain error), `HealthTimeouts` (named WMI /
Event Log timeouts), an internal `WmiReader` (query with timeout -> property dictionaries).

**Disk health**
- `DiskHealthInfo` is the raw reading for one physical disk: friendly name, media type, size,
  `MSFT_PhysicalDisk.HealthStatus` / `OperationalStatus`, and (nullable, often missing without admin
  or on USB disks) `MSFT_StorageReliabilityCounter` temperature, wear (percent of life used) and
  uncorrected read errors, plus `PredictFailure`.
- `DiskHealthEvaluator.Evaluate` (pure) returns a `DiskHealthVerdict` - `Healthy`, `Warning`,
  `Unknown` - with the display text "Healthy", "Warning - back up your files soon", "Unknown" and a
  list of plain reasons ("Windows reports this drive as unhealthy", "This drive has used 92% of its
  life", "Running hot (72 °C)"). Warning when: `PredictFailure` is true; HealthStatus is Warning or
  Unhealthy; OperationalStatus contains Degraded / Predictive Failure / Error / No contact / Lost
  communication; wear >= 90%; uncorrected read errors > 0; temperature >= 70 °C. Healthy only when
  HealthStatus is Healthy and nothing above fires. Otherwise Unknown.
- `root\wmi` `MSStorageDriver_FailurePredictStatus` cannot be reliably mapped to one
  `MSFT_PhysicalDisk` when several disks exist, so it is applied to a disk only when the PC has a
  single physical disk; with several, `DiskHealthSnapshot.PredictFailureDetected` is a card-level
  warning ("Windows expects one of your drives to fail").
- `IDiskHealthService.GetAsync` -> `HealthReadResult<DiskHealthSnapshot>`; `WmiDiskHealthService`
  is the real implementation.

**Windows repair**
- `IWindowsRepairService.RunSfcAsync` / `RunDismAsync` run `%SystemRoot%\System32\sfc.exe /scannow`
  and `dism.exe /Online /Cleanup-Image /RestoreHealth` through `IProcessRunner`, streaming an
  `IProgress<RepairProgress>` (percentage when the output has one, plus the cleaned output line).
- **Encoding:** `sfc.exe` writes UTF-16. `IProcessRunner` decodes stdout as UTF-8, so the text
  arrives with NUL characters between letters; `RepairOutputCleaner` strips them before any
  parsing. (Non-ASCII localised output cannot be recovered this way - see Non-goals.)
- `SfcOutputParser`: `Parse` -> `SfcOutcome` (`NoProblems`, `Repaired`, `CouldNotRepair`,
  `RebootPending`, `CouldNotRun`, `Unknown`) from the Windows Resource Protection summary sentence;
  `TryParseProgress` reads "Verification 45% complete.".
- `DismOutputParser`: `Parse(exitCode, lines)` -> `DismOutcome` (`Succeeded`, `SourceNotFound`,
  `NeedsAdmin`, `Failed`); `TryParseProgress` reads `[====  45.0%  ]`.
- `RepairOutcomeDescriber` turns an outcome into one plain sentence. The flow: SFC first; **only** if
  it reports `CouldNotRepair` does the page offer DISM, and after a successful DISM it suggests
  running the SFC check once more.
- Neither tool is cancellable once started (killing SFC/DISM mid-write can leave the component
  store worse off): the token is only honoured before launch, as documented on
  `IProcessRunner.RunAsync`. The page warns "This can take 10 to 30 minutes" and registers as
  `IBusyGuard` so closing the window asks for confirmation.

**Restore point**
- `IRestorePointService`: `GetStatusAsync` (System Protection on/off, frequency limit, the most
  recent restore points - read-only) and `CreateAsync(description, kind)` calling WMI
  `root\default` `SystemRestore.CreateRestorePoint` (`RestorePointKind.ApplicationInstall` = 0 or
  `ModifySettings` = 12, event type BEGIN_SYSTEM_CHANGE).
- System Protection is read from the registry (`SystemRestore\RPSessionInterval`, policy
  `DisableSR`). When off, the card says so plainly and offers "Open System Protection"
  (`SystemPropertiesProtection.exe`); Create is disabled.
- **Frequency limit:** Windows silently skips creating a restore point if one was made within
  `SystemRestorePointCreationFrequency` minutes (default 1440 = 24 h; 0 = no limit) while still
  returning success. `RestorePointRules` (pure) computes whether creating is currently allowed and
  when it will be; the card explains it, and after creating, the service re-reads the list and
  reports `NotCreatedTooSoon` if nothing new appeared instead of claiming success.
- `RestorePointRules.DescribeError` maps WMI return codes (e.g. 1058 service disabled) to plain text.

**Recent problems**
- `HealthEventRecord` (log, provider, event id, time, string properties) is what
  `IProblemEventReader` returns for the last 30 days (real impl: `System.Diagnostics.Eventing.Reader`
  with an XPath filter, capped at a maximum record count, with an overall timeout).
- `ProblemSummarizer` (pure) maps and groups records into `ProblemSummary` rows:

| Category | Source | Plain text |
|---|---|---|
| App crash | Application: `Application Error` 1000 (app name = property 0); `Windows Error Reporting` 1001 `APPCRASH` only when no matching 1000 within 2 minutes | "Chrome closed unexpectedly 4 times" (grouped by app) |
| Blue screen | System: `Microsoft-Windows-WER-SystemErrorReporting` 1001 | "Windows showed a blue screen and restarted 2 times" |
| Unexpected shutdown | System: `Microsoft-Windows-Kernel-Power` 41, `EventLog` 6008 (merged when within 10 minutes) | "The PC shut down unexpectedly (power loss or freeze) 3 times" |
| Disk error | System: `disk` 7 / 51 / 153, `Ntfs` 55 | "Windows had trouble reading or writing a drive 5 times" |
| Failed update | System: `Microsoft-Windows-WindowsUpdateClient` 20 | "A Windows update failed to install 1 time" |

  Each row has a count, the last time it happened, and (for the serious ones) one line of advice.
  Rows are ordered blue screens, disk errors, unexpected shutdowns, failed updates, then app crashes
  by count. No problems -> "No problems found in the last 30 days".

**Battery**
- `BatteryReading` (design capacity mWh, full-charge capacity mWh, cycle count?, charge percent?,
  `BatteryChargeState`) from `root\wmi` `BatteryStaticData.DesignedCapacity`,
  `BatteryFullChargedCapacity.FullChargedCapacity`, `BatteryCycleCount.CycleCount` and
  `Win32_Battery` (`EstimatedChargeRemaining`, `BatteryStatus`).
- `BatteryHealthCalculator` (pure): health % = full / design, clamped to 0-100 (null when either is
  missing or zero); verdict `Good` (>= 80%), `Worn` (50-79%: "Worn - holds about 60% of its original
  charge"), `VeryWorn` (< 50%: "... consider replacing the battery"), `Unknown`.
- `IBatteryService.GetAsync` returns `HealthReadResult<BatteryReading?>`; `null` value = no battery,
  and the card stays hidden.

`AddHealthCore()` registers the services; in DEBUG with `PORCHLIGHT_DEMO_DATA=1` it registers fakes
(healthy + one warning disk, sample restore points and problems, a worn battery, a restore point
"creation" that only reports success).

### App (`Porchlight.App.Features.Health`)

- `HealthViewModel` (`PageViewModelBase`, `IBusyGuard`, Title "Health check", Order 6) owns five card
  view models and refreshes them on first navigation and on "Check again". Each card has its own
  checking / error state so one failure never hides the others.
- `HealthView` is a scrolling column of `Card` borders (works from 900x600). The admin banner shows
  once at the top when not elevated; repair and Create buttons are disabled with it visible.
- Long work (SFC/DISM) shows an indeterminate/percentage bar, the current step, and a collapsible log.
- `HealthFeature.AddHealthFeature()` is the single DI extension; `App.xaml.cs` gets one line.

## Acceptance criteria

1. "Health check" appears in the nav rail at order 6 and renders at 900x600 without horizontal
   overflow, in light and dark.
2. Disk card lists every physical disk with name, type, size and a verdict shown as icon + text;
   unit tests cover every verdict rule above, including missing (null) reliability data.
3. A WMI failure or timeout in any card shows "Couldn't check" for that card only.
4. Windows repair: SFC runs first with live progress; DISM is offered only after an SFC
   `CouldNotRepair`; both need admin (banner + disabled buttons when not elevated); the 10-30 minute
   warning is shown; there is no cancel button; closing the app mid-run asks for confirmation.
   Parser tests cover SFC UTF-16-as-UTF-8 (NUL-interleaved) text, each SFC outcome, and DISM progress,
   success and failure.
5. Restore point: shows System Protection state, the frequency limit and the next allowed time, the
   most recent restore points, and a working "Open System Protection" link; Create is disabled when
   protection is off or the limit has not elapsed, and never reports success when nothing was created.
6. Recent problems: fake-record tests cover grouping by app, dedupe of 1000 vs 1001 and of 41 vs
   6008, the 30-day window, ordering, and plural/singular wording.
7. Battery card is hidden on a device with no battery; health %, verdict and cycle count (when
   available) are shown otherwise; calculator tests cover clamping and every band.
8. `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes; README "Features"
   updated and the planned "System health" module / "Battery health" item removed.
