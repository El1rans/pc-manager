# 10 - Readable sensors screen (branch `feat/readable-sensors`)

The maintainer tested Hardware -> Sensors as admin (PawnIO installed, AMD Ryzen 7 5800X3D, Nuvoton NCT6798D, RTX 4070 SUPER) and found it unreadable.

## Problems observed (screenshot, 2026-09-28)

1. **Inverted emphasis**: the Current column and sensor names render in a dim/disabled-looking brush, while Min/Max are bright white. Current is the most important value.
2. **One long flat list** per hardware: voltages, fans, loads, clocks, power, factors all mixed; temperatures buried at the bottom.
3. **Noise**: unconnected headers shown as "Fan #1/#4-#7 0 RPM (min 0, max 0)" and many "Voltage #n" rows.
4. **Misaligned columns** between hardware groups (different indentation/column offsets); the Current/Min/Max header is repeated per group with different positions.
5. **Missing/odd units**: e.g. "Core #1  34" (no unit - probably a Factor/multiplier or a temperature without °C; identify the SensorType and format correctly).
6. Every CPU core shows Current "100 %" load while Min/Max vary. This may be genuine (another process was stressing the CPU at that moment) - VERIFY by comparing against Task Manager / the Dashboard under idle load; if Current is bound to the wrong value, fix it.

## Design

- **Summary strip at the top** ("At a glance"): up to 4 tiles - CPU temperature (package / Tctl-Tdie), GPU temperature (core; hot spot as detail), CPU package power, and the hottest fan RPM or "Fans: 2 active". Reuse the Dashboard tile style (Card, big value, icon + text for warnings: e.g. >= 85 C caution, >= failsafe critical, using the same thresholds as fan control's failsafe setting).
- **One card per hardware device** (Expander, header = device name + type icon; CPU and GPU expanded by default, others collapsed), in a sensible order: CPU, GPU, Motherboard, Memory, Storage, Network.
- **Inside each card, sections by sensor type** in this order: Temperatures, Fans, Load, Power, Clocks, Voltages, then the rest (Data, Throughput, Factor, Level, Energy, Current, SmallData...). Each section has a small heading with its unit/icon; empty sections are hidden.
- **Table layout** with aligned columns across ALL cards (Grid.IsSharedSizeScope or fixed column widths): Name | Current | Min | Max. Current is primary text brush + SemiBold; Name is primary text brush; Min/Max use TextFillColorSecondaryBrush. Numbers right-aligned with tabular figures (Typography.NumeralAlignment="Tabular" or a monospace-digit font) so values don't jitter every second. One column header row per card (or sticky at the top), not per section.
- **Units**: temperature "°C" with 0-1 decimals, fan "RPM" (integer), load "%" (integer or 1 decimal), clock "MHz"/"GHz", power "W", voltage "V" (3 decimals for small voltages), factor "×" with 2 decimals, data per the existing rules (SmallData MB, Data GB, Throughput KB/s/MB/s). Every SensorType has a unit or explicit formatting; add a unit test covering every LibreHardwareMonitor SensorType value.
- **"Hide unused sensors"** toggle, ON by default (persisted in Hardware settings via ISettingsStore.Update): hides sensors whose value has been null or 0 for their whole observed history (e.g. unconnected fan headers, 0 V inputs). Never hides temperatures that report a real value.
- Keep: Filter box (filters across all cards, auto-expands matching cards), Reset min/max.
- Performance: values update in place (existing stable-VM approach); no layout jumps; UI virtualization where lists are long.
- Works at 900x600 without horizontal scrolling; readable in light and dark theme; status is never color-only.
- DEBUG demo-data mode: update the demo IHardwareService so the new layout has realistic demo data for screenshots (docs/screenshots/hardware.png).

## Out of scope

Fans tab changes (except sharing the new summary tiles if trivial), fan-control logic (must remain untouched - rename/UI only).

## Tests

Unit formatter for every SensorType; section ordering/grouping; "hide unused" rule (0/null-only history hidden, any real value shown, temperatures never hidden when valid); summary-tile selection (Tctl/Tdie preferred on AMD, Package on Intel, GPU core + hot spot).

## Acceptance criteria

- [ ] Current values are the most prominent text; nothing looks disabled.
- [ ] Temperatures appear first; a user finds CPU and GPU temperature within 2 seconds (summary strip).
- [ ] Columns align across every card; all values have correct units.
- [ ] Unused sensors hidden by default, toggle to show them.
- [ ] The 100 % current-load observation is verified (bug fixed or explained).

## Addendum: collapsed-card stats (maintainer request, 2026-09-28)

A collapsed device card (Motherboard, Memory, Storage, Network, and anything else collapsed by
default) shows only the device name until expanded - the maintainer asked for a couple of live
numbers next to the name so a glance at the collapsed list is useful without expanding every card.

- When a card is **collapsed**, its header shows the device's 2 most useful live stats next to its
  name, right-aligned before the expand chevron, e.g.
  `AMD Ryzen 7 5800X3D      69.8 °C · 22 %`. Values are formatted with `SensorFormatter`, joined
  with " · ", secondary foreground, single line (`TextTrimming="CharacterEllipsis"`, no wrap) so it
  still works at 900x600.
- When a card is **expanded**, the stats are hidden - the same values are visible in the sensor
  rows below, so showing both would be redundant.
- The pair is picked per hardware type by `HardwareCardStatsSelector` (`Porchlight.Core.Hardware`),
  a pure function next to `HardwareSummarySelector`, reusing its CPU/GPU temperature pickers so the
  summary strip and a collapsed CPU/GPU card never disagree about which sensor is "the"
  temperature:
  - **CPU**: main temperature (`HardwareSummarySelector.SelectCpuTemperature`) + "CPU Total" load.
  - **GPU**: GPU core temperature (`SelectGpuTemperature`) + GPU core load.
  - **Memory**: memory load % + "Memory Used" (Data, GB).
  - **Storage**: temperature + "Used Space" (Load, %) - not the separate "Used Space" Data (GB)
    sensor some storage devices also report.
  - **Motherboard/SuperIO**: hottest valid temperature (never an inverted one) + fastest fan RPM
    (ignoring 0 RPM unconnected headers, same rule as `SelectHottestFan`).
  - **Network**: download + upload throughput, matched by sensor name where possible, falling back
    to the first two distinct Throughput sensors.
  - **Other/fallback**: first temperature + first load.
  - If a type has fewer than 2 usable readings, the card shows what exists; with none, it shows
    nothing (no empty separator, no placeholder).
- Recomputed every snapshot tick alongside the rest of the card (`HardwareCardViewModel.UpdateFrom`),
  so the stats update live like every other sensor value.
- Accessible: `AutomationProperties.Name` on the header includes the stats while collapsed (e.g.
  "AMD Ryzen 7 5800X3D, 69.8 °C, 22 %"), and falls back to just the device name once expanded or
  when there is nothing to show. No status is conveyed by color alone here - the collapsed stats
  are plain secondary text, not colored by severity (the summary strip above already owns
  caution/critical coloring for the same readings).
