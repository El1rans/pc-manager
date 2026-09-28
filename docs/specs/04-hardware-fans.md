# 04 - Hardware sensors and fan control (branch `feat/hardware`)

> **Amended after 01b:** the PawnIO driver is the `pawnio` component from milestone 01b. When it is missing, show the shared `ComponentCard` for it (install in place via `IComponentService`, then initialize hardware access without restarting the app, re-opening `Computer` if needed). Replace the "card explaining how to install it" requirement below with this.

Goal: show temperatures, fan speeds, clocks, loads, voltages and power for all hardware, and let the user control fans with curves, with safety that cannot be turned off.

**Fan control can overheat and damage hardware if done wrong. The safety rules below are requirements, not suggestions.**

## Hardware access

- Library: `LibreHardwareMonitorLib` latest stable (0.9.6 at time of writing). Verify on its GitHub README/release notes which kernel driver that version uses (recent versions moved from the WinRing0 driver, which Microsoft Defender flags as vulnerable, to PawnIO). Never bundle or install WinRing0.
- Most sensors and all fan control need the app to run as administrator and the driver to be available. Without them, show what is readable (e.g. GPU via vendor APIs, drives) plus the `AdminRequiredBanner`, and if the driver is missing, a card explaining how to install it (link to the official source; if a winget package exists for it, show the exact `winget install` command). Do not install drivers automatically.
- All library calls happen on one dedicated background thread/owner (`HardwareService` singleton); `Computer.Open()` once, `Update()` every 1s via a visitor, `Close()` on shutdown. The UI receives immutable snapshots.

## Core (`Porchlight.Core/Hardware`)

- `IHardwareService`: `Start()`, `Stop()`, `IObservable`-or-event of `HardwareSnapshot` (list of `HardwareNode(Id, Name, Type, Sensors[], Children[])`, `SensorReading(Id, Name, SensorType, Value?, Min?, Max?)`), `Status` (NotElevated, DriverMissing, Ready, Error + message), and `IReadOnlyList<IFanController> Controllers`.
- `IFanController`: `Id`, `Name`, `CurrentPercent`, `CanControl`, `SetPercent(double)`, `RestoreDefault()`. The LHM implementation maps to `ISensor.Control` (`SetSoftware`, `SetDefault`).
- `FanCurve`: sorted points `(temperatureC, percent)`, linear interpolation, flat beyond the ends. Validation: 2-8 points, temperatures strictly increasing, percent in [MinPercent, 100], percent non-decreasing. Hysteresis: only lower the fan speed when temperature drops at least 3 C below the point where the current speed was set.
- `FanControlEngine` (pure logic, fully unit tested, no LHM types): input = current temperatures by sensor id + fan assignments; output = target percent per fan. Rules:
  1. **Minimum floor**: `MinPercent` = 30% default, the user cannot set it below 20%.
  2. **Failsafe - overheat**: if any CPU or GPU temperature >= `FailsafeTemperatureC` (default 90 C, user range 70-95 C), every controlled fan goes to 100% until all temperatures are 10 C below the threshold.
  3. **Failsafe - lost sensor**: if a fan's source temperature is missing, NaN, or stale (> 5 s old), that fan goes to 100%.
  4. **Failsafe - errors**: if setting a fan throws, restore all fans to default (BIOS) control and disable software control, log it, and show a critical banner.
  5. **Restore on exit**: on app exit, crash handler, session end, and system suspend (`SystemEvents.PowerModeChanged` / `SessionEnding`), every controlled fan gets `RestoreDefault()`.
- Fan control is OFF by default. Turning it on requires: admin, driver ready, and a one-time confirmation dialog explaining the risks. The "enabled" flag is persisted but software control only resumes on next launch after the page shows it is active (no silent control at startup without the page having loaded the profile successfully).
- Fan profiles persist in the `Hardware` settings section: per fan: mode (`Default` = BIOS, `Fixed` percent, `Curve` with source sensor id + points).

## App (`Porchlight.App/Features/Hardware`)

- **Sensors tab**: tree grouped by hardware (CPU, GPU, Motherboard, Memory, Storage, Network), each sensor with current / min / max and unit (C, RPM, %, MHz, V, W). Filter box. "Reset min/max".
- **Fans tab**: one card per fan with current RPM and %, mode selector (Default / Fixed / Curve), fixed slider (floor enforced), curve editor (a small chart with draggable points; points snap to 1 C and 1%), source sensor picker (temperature sensors only). Global: master "Software fan control" toggle, failsafe temperature, minimum percent, big "Restore BIOS control for all fans" button.
- Safety banners: critical banner when failsafe is active (which rule, which sensor), caution banner when running without admin/driver.
- Dashboard integration is out of scope for this milestone (a follow-up adds CPU/GPU temperature tiles).

## Tests

`FanCurve` interpolation, validation, hysteresis; `FanControlEngine` rules 1-4 with fake inputs and a fake clock; profile (de)serialization round trip.

## Acceptance criteria

- [ ] Without admin: page loads, shows banner, no crash; sensors that are readable are shown.
- [ ] As admin with driver: sensors tree populates and updates every second.
- [ ] Setting a fixed speed on a controllable fan changes its RPM; closing the app returns it to BIOS control.
- [ ] Unit tests prove every failsafe rule.
- [ ] Fan control is never active unless the user enabled it and confirmed the warning.

## Addendum: Fans tab polish and conflict detection

From maintainer testing on an ASUS ROG STRIX B550-F with Armoury Crate installed. All additive -
none of these change or weaken a failsafe rule above.

### Hiding empty fan headers

Reuses the Sensors tab's "Hide unused sensors" concept and setting (`UnusedSensorTracker`,
`HardwareSettings.HideUnusedSensors`) rather than adding a second toggle: a fan card is hidden on
the Fans tab exactly when the setting is on *and* its paired RPM sensor's id has never reported a
value greater than 0 across the whole observed session (`UnusedSensorTracker.Observe`/`IsEverUsed`).
Rules:

- A fan that has ever reported RPM > 0 stays visible forever after, even once it idles back to 0
  (e.g. a case fan at low load) - the same "once real, always shown" rule the Sensors tab already
  uses for every other sensor type.
- A GPU fan (any `IFanController` whose `NodeType` is `HardwareNodeType.Gpu` - derived in
  `HardwareService.BuildNode` from the same `hardware.HardwareType` the node itself is mapped
  from) is **never** hidden, regardless of RPM history. GPUs commonly run a legitimate 0-RPM
  "idle" fan mode; hiding that fan would look like it disappeared. When such a fan reads 0 RPM its
  card shows "Stopped (idle)" instead of "0 RPM" (`FanCardViewModel.RpmDisplayText`).
- A fan control channel with no matching RPM/tachometer sensor at all is never hidden either -
  there is no reading to judge history from.

### Per-fan custom display names

`HardwareSettings.FanDisplayNames` is a `Dictionary<string, string>` keyed by the fan's **stable
controller id** (`IFanController.Id` - the same id `FanProfiles` is already keyed by), never an
index, since a hardware re-enumeration can shift indices but not this id. An empty/missing entry
means "use the hardware-reported name".

The Fans tab has an inline "Rename" button per card; committing an edit writes (or, if cleared,
removes) the entry immediately, the same "no separate Apply step" pattern the rest of the page
already follows. The original hardware-reported name is always shown as secondary/subtitle text
once a custom name is set.

`Porchlight.Core.Hardware.FanNaming.ResolveDisplayName(fanId, hardwareName, customNames)` is the
one fallback rule every place a fan name is shown goes through:

- The Fans tab card itself (`FanCardViewModel.DisplayName`).
- Sensors tab RPM rows - `HardwareViewModel` builds a one-tick map of RPM-sensor-id -> custom name
  from the controllers' `RpmSensorId`s and threads it through `HardwareCardViewModel` /
  `SensorSectionViewModel` / `SensorRowViewModel` as an optional override, so the Sensors tab's own
  XAML and layout needed no changes (deliberately kept minimal to avoid conflicting with the
  Sensors-tab work in PR #27).
- The "Hottest fan" "At a glance" summary tile (`HardwareSummarySelector.Build`'s optional
  `fanDisplayNameOverrides` parameter).

### Units display fix

The failsafe temperature slider's value and its label now read "90 °C" and "Overheat failsafe
temperature (°C)" (previously "90 C" / "Overheat failsafe temperature (C)").

### Conflicting fan-control software detection

A new Core service, `IFanControlConflictDetector` (`FanControlConflictDetector`), detects known
vendor fan-control tools that write to the same fan channels Porchlight does, so a warning can be
shown rather than the two silently fighting each other. Detection only - it never stops, kills,
disables, or otherwise modifies anything it finds.

It reads from a thin, injectable `IRunningSoftwareLister` abstraction (`GetRunningProcessNames`,
`GetRunningServiceNames`) rather than calling `Process`/WMI directly, so the detector is fully unit
testable with a fake lister. The real implementation (`RunningSoftwareLister`) uses
`Process.GetProcesses()` for process names and a `Win32_Service` WMI query (the same technique
`Monitoring.SystemInfoProvider` already uses) for running Windows service names.

Known software, matched case-insensitively by process name and/or service name
(`FanControlConflictDetector.KnownSoftware`):

- ASUS Fan Control / Armoury Crate (`AsusFanControlService`, `ArmouryCrate.Service`)
- MSI Center / Dragon Center
- Gigabyte SIV / Control Center
- FanControl (Rem0o's FanControl.exe)
- SpeedFan
- Argus Monitor
- iCUE (Corsair Commander)
- NZXT CAM
- Lian Li L-Connect

When one or more are detected, the Fans tab shows a caution banner: "*&lt;name&gt;* is also
controlling your fans. Porchlight's settings may be overridden. Close/disable it to use
Porchlight fan control." Detection re-runs when the Hardware page first loads and again every time
the Fans tab becomes the selected tab (`HardwareView`'s `TabControl.SelectionChanged`), rather than
on every one-second snapshot tick, since process/service enumeration is comparatively expensive.

### Logging

`FanControlManager` now logs at Information level (previously only failures were logged):

- Software fan control being armed (`Activate`/`Rearm`).
- Fan control being paused - including the exit/suspend/session-end restore path - and each fan
  successfully restored to BIOS/default control.

`FanCardViewModel` logs at Information level when the user changes a fan's mode or fixed target
duty (not on every enforcement tick - only on the user-driven setting change, to avoid log spam).
`FanControlConflictDetector` logs each detected conflict at Information level.
