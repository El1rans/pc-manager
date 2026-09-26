# 03 - Dashboard (branch `feat/dashboard`)

Goal: a live overview of the PC that is cheap to run (target: under 2% CPU on a typical 8-core machine while the dashboard is visible).

## Core (`PCManager.Core/Monitoring`)

- `ISystemInfoProvider.GetAsync(ct)` -> `SystemInfo` (computer name, OS caption + build, device manufacturer/model, CPU name, physical cores, logical processors, GPU names, total RAM, last boot time). Uses WMI (`System.Management`): `Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_Processor`, `Win32_VideoController`. Runs off the UI thread; any single query failing leaves that field "Unknown".
- `IPerformanceSampler.Sample()` -> `PerformanceSnapshot` with nullable values where a source is unavailable:
  - CPU %: `Processor Information / % Processor Utility / _Total` (matches Task Manager), fallback `Processor / % Processor Time`. Clamp 0-100.
  - Memory: `GlobalMemoryStatusEx` (used, total).
  - GPU %: `GPU Engine / Utilization Percentage` read in ONE call with `PerformanceCounterCategory.ReadCategory()`, computed per instance with `CounterSample.Calculate(previous, current)`, summed per engine type (`engtype_3D`, `engtype_VideoDecode`, ...), reported as the max over engine types (Task Manager's method). Clamp 0-100.
  - Disk: `PhysicalDisk / _Total`: active % = 100 - `% Idle Time`, plus read and write bytes/sec.
  - Network: sum of `GetIPStatistics()` byte deltas over interfaces that are Up and not Loopback/Tunnel, divided by elapsed time (use `Stopwatch` timestamps).
  - Counters are created once, primed, and disposed with the sampler.
- `IProcessMonitor.SampleTop(int count)` -> top processes grouped by process name (count, total CPU %, total working set). CPU from `TotalProcessorTime` deltas / elapsed / logical processor count. Cache PIDs that throw access denied so they are not retried every tick. Sort by CPU desc then memory desc.
- `IDriveMonitor.GetDrives()` -> fixed and removable ready drives: name, label, format, total, free, `IsLow` (free < 10% or < 10 GB).
- `IRestartDetector.IsRestartPending()`: registry keys `...\Component Based Servicing\RebootPending` or `...\WindowsUpdate\Auto Update\RebootRequired`.
- `RollingSeries` (fixed capacity 60): push, snapshot as `double[]`, min/avg/max. Unit tested.
- `ByteFormatter`: bytes (`1.2 GB`), byte rates (`1.2 MB/s`), bit rates for network (`12.3 Mbps`), durations (`3d 4h 12m`). Unit tested, invariant of current culture where it matters.

## App (`PCManager.App/Features/Dashboard`)

- A sampling loop (`PeriodicTimer`, 1s) runs on a background task and publishes results to the UI thread. It starts when the app starts (history is kept while on other pages) and pauses while the window is minimized.
  - Every tick: performance snapshot. Every 2 ticks: top processes. Every 15 ticks: drives. Every 60 ticks: restart check.
- Layout:
  - Header: "Dashboard", subtitle "<computer name> - <OS> - up <uptime>". If a restart is pending, a caution badge with a warning icon and "Restart pending".
  - Metric tiles in a 3-column grid: CPU, Memory, GPU, Disk, Download, Upload. Each tile: title, big value, one detail line (e.g. "9.8 of 32 GB", "Read 2 MB/s - Write 1 MB/s"), and a `Sparkline` of the last 60 seconds. Percent tiles use a fixed 0-100 scale; throughput tiles auto-scale. Tooltip on the tile: "Last 60s - min X, avg Y, max Z". Unavailable source: value "n/a" and detail "Not available on this PC".
  - Row of two cards: "Drives" (name + label, "X free of Y", progress bar of used space, low-space caution icon + "Low space" label) and "Top processes" (name with count, CPU %, memory; 8 rows).
  - "System" card: two-column label/value list from `SystemInfo`.
- The existing `Controls/Sparkline.cs` draft is the chart; keep its visual rules (2px line, 18% area fill, faint baseline, accent brush).

## Tests

`RollingSeries`, `ByteFormatter`, the GPU engine-type aggregation (pure function over `(instanceName, value)` pairs), process CPU % math (pure function), `IsLow` rule.

## Acceptance criteria

- [ ] All values update every second without UI stutter; CPU usage of PC Manager itself stays low.
- [ ] Numbers roughly match Task Manager (CPU, memory, disk, network within reasonable tolerance).
- [ ] Works on a PC without a GPU counter category (GPU tile shows n/a).
- [ ] Minimizing pauses sampling; restoring resumes.
