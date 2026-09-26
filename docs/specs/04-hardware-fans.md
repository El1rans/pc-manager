# 04 - Hardware sensors and fan control (branch `feat/hardware`)

Goal: show temperatures, fan speeds, clocks, loads, voltages and power for all hardware, and let the user control fans with curves, with safety that cannot be turned off.

**Fan control can overheat and damage hardware if done wrong. The safety rules below are requirements, not suggestions.**

## Hardware access

- Library: `LibreHardwareMonitorLib` latest stable (0.9.6 at time of writing). Verify on its GitHub README/release notes which kernel driver that version uses (recent versions moved from the WinRing0 driver, which Microsoft Defender flags as vulnerable, to PawnIO). Never bundle or install WinRing0.
- Most sensors and all fan control need the app to run as administrator and the driver to be available. Without them, show what is readable (e.g. GPU via vendor APIs, drives) plus the `AdminRequiredBanner`, and if the driver is missing, a card explaining how to install it (link to the official source; if a winget package exists for it, show the exact `winget install` command). Do not install drivers automatically.
- All library calls happen on one dedicated background thread/owner (`HardwareService` singleton); `Computer.Open()` once, `Update()` every 1s via a visitor, `Close()` on shutdown. The UI receives immutable snapshots.

## Core (`PCManager.Core/Hardware`)

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

## App (`PCManager.App/Features/Hardware`)

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
