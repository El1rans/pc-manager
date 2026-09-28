# SignPath Foundation application - draft

This is a ready-to-paste draft of the answers for the SignPath Foundation application at
<https://signpath.org/apply>. **Nothing here has been submitted.** The maintainer should review,
adjust, and submit it manually (the application form itself asks the applicant to sign in with
GitHub and fill these in).

---

**Project name:** Porchlight

**Repository URL:** https://github.com/El1rans/porchlight

**License:** MIT (see `LICENSE`) - OSI-approved, no dual/commercial licensing.

**Project description:**

> Porchlight is a Windows 10/11 desktop app (WPF, .NET 10) for monitoring and maintaining a single
> family PC. It shows live system stats (CPU/memory/GPU/disk/network usage, temperatures, top
> processes), lists and installs app updates via Windows' own `winget` package manager, reads
> hardware sensors and can control fan speed, controls RGB lighting on supported devices via
> OpenRGB, and offers a plain-language "Get help" page for a family member to receive remote
> support via AnyDesk. It is aimed at a non-technical relative's PC that a more technical family
> member maintains remotely.

**What the software does, honestly, including the parts a reviewer should know about:**

- The installer and an in-app "Set up optional features" page can install three optional
  third-party tools via `winget`, **only with the user's explicit, per-component consent**
  (unticked by default except AnyDesk in the installer's default selection): AnyDesk (remote
  support), OpenRGB (RGB lighting control), and the PawnIO driver (namazso.PawnIO - a
  community-signed kernel driver Porchlight uses for low-level sensor/fan access). None of these
  are bundled inside Porchlight's own installer payload; they are always fetched via `winget` from
  their own publishers at install time, over the network, with the user's consent.
- The app can control fan speed in software (a "Fixed" or custom "Curve" mode per fan). This is
  off by default, requires a one-time explicit risk acknowledgement in the UI, only runs while a
  documented safety engine reports the page is fully healthy (elevated process, driver present,
  sensors readable), and always restores fans to BIOS/hardware control on exit, crash, sleep, or
  session end. The safety engine enforces a minimum speed floor and shuts fan control down on
  overheat or lost-sensor conditions. This is disclosed prominently in the UI and in
  `README.md`/`docs/specs/` before a user can turn it on.
- The app requests administrator privileges for the parts of it that need them (per-machine
  install, the PawnIO driver, full sensor/fan access) and shows a clear "Restart as administrator"
  banner rather than silently failing when unelevated.
- Network activity is limited to: `winget` (Microsoft's own package manager, invoked as a child
  process) - for ticked/selected components, for updates the user selects, **and automatically in
  the background on every app start** to list available updates (a "Check for updates when
  Porchlight starts" toggle, on by default, controls this; see Microsoft's
  [privacy statement](https://privacy.microsoft.com/privacystatement) for what winget's own
  network calls send); a local-only (`127.0.0.1` by default, configurable) TCP connection to a locally running
  OpenRGB SDK server; AnyDesk's own network traffic whenever the locally installed AnyDesk process
  is running (see [AnyDesk's privacy policy](https://anydesk.com/en/privacy)); and the user's
  default browser opening a documented external URL (`https://anydesk.com/download`) only when the
  user clicks "Download AnyDesk manually". There is no telemetry, analytics, or Porchlight-operated
  call-home server. Full detail: `docs/CODE_SIGNING_POLICY.md`'s privacy statement section.
- The installer includes a documented, working uninstaller ("Apps & features" or the Start Menu
  shortcut) that removes Porchlight and, on request, its per-user settings/logs. It intentionally
  does not remove the separately-installed third-party tools (AnyDesk/OpenRGB/PawnIO), since those
  are independent applications the user may still want - this is disclosed on the uninstall finish
  page.

**Third-party components (all permissively licensed, none copyleft-restrictive):**

| Component | License | How it's used |
|---|---|---|
| `src/ThirdParty/OpenRGB.NET` (vendored, by Diogo Trindade) | MIT | RGB lighting control (OpenRGB SDK client) |
| `LibreHardwareMonitorLib` | MPL-2.0 | Hardware sensor reading (temperatures, fans, voltages) |
| `CommunityToolkit.Mvvm` | MIT | MVVM source generators (`[ObservableProperty]`, etc.) |
| `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.File` | Apache-2.0 | Local file logging under `%APPDATA%\Porchlight\logs` |
| `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging.Abstractions` | MIT | .NET generic host / DI / logging abstractions |
| `System.Management`, `System.Diagnostics.PerformanceCounter` | MIT | WMI and performance-counter based system info |
| `xunit.v3` (Apache-2.0), `Microsoft.Extensions.TimeProvider.Testing` (MIT) | test-only, not shipped | Test framework and fake-clock testing helper |

**Build system:** GitHub Actions, on GitHub-hosted `windows-latest` runners
(`.github/workflows/release.yml`), triggered only by a `vX.Y.Z` tag pushed to this repository after
it has been merged to `main` through a reviewed pull request. The workflow builds from source,
runs the test suite, publishes a self-contained single-file build, compiles the Inno Setup
installer, and (once approved) submits both the published executable and the compiled installer to
SignPath for signing before creating the GitHub Release.

**Maintainer / team:** Single maintainer, [@El1rans](https://github.com/El1rans), who is also the
Committer and Reviewer for all changes (see `docs/CODE_SIGNING_POLICY.md`). Development is
AI-assisted (changes are drafted with Claude Code as a coding agent), but every change goes through
a pull request with required CI checks and is reviewed by the maintainer before merge (no formal
self-approval exists). All team members (currently just the maintainer) use MFA on GitHub and
SignPath.

**Uninstall:** Standard Windows uninstall via "Apps & features", built from the Inno Setup script
(`installer/Porchlight.iss`); also available from the Start Menu group.

**Data transfer disclosure:** see the privacy statement and its supporting detail in
`docs/CODE_SIGNING_POLICY.md`.
