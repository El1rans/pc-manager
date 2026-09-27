# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Foundation: solution layout, host + DI, Serilog file logging, JSON settings store with atomic
  writes and corrupt-file recovery, WPF shell with navigation rail (Dashboard, Updates, Hardware,
  Lighting placeholders), admin elevation support, CI workflow and repo hygiene files.
- Dashboard: a live overview of the PC, sampled once a second off the UI thread and paused while
  the window is minimized. Metric tiles for CPU, memory, GPU, disk and network throughput with
  60-second sparklines and min/avg/max tooltips; a drives card with a low-space warning; a top
  processes card; a restart-pending badge; and a system info card (computer name, OS, CPU, GPU,
  RAM). Measured self-CPU stays under 1% of total system CPU on an 8-core/16-thread machine.

### Fixed

- `SettingsStore.Save`/`Update` no longer throw when the settings file is transiently locked by
  another process (e.g. Defender, the Search indexer, or OneDrive briefly holding the file during
  the atomic rename). The final move now retries with bounded backoff, and if it still fails,
  persistence is treated as best-effort: a warning is logged, in-memory `Current` stays
  authoritative, and the next save retries. This fixes intermittent `UnauthorizedAccessException`
  crashes on startup (`PC Manager could not start`) caused by the launch-count update.
