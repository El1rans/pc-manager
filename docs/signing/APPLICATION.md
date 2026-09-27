# SignPath Foundation application - draft

This is a ready-to-paste draft of the answers for the SignPath Foundation application at
<https://signpath.org/apply>. **Nothing here has been submitted.** The maintainer should review,
adjust, and submit it manually (the application form itself asks the applicant to sign in with
GitHub and fill these in).

---

**Project name:** PC Manager

**Repository URL:** https://github.com/El1rans/pc-manager

**License:** MIT (see `LICENSE`) - OSI-approved, no dual/commercial licensing.

**Project description:**

> PC Manager is a Windows 10/11 desktop app (WPF, .NET 10) for monitoring and maintaining a single
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
  community-signed kernel driver PC Manager uses for low-level sensor/fan access). None of these
  are bundled inside PC Manager's own installer payload; they are always fetched via `winget` from
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
  process, only for ticked/selected components or updates the user selects), a local-only
  (`127.0.0.1`) TCP connection to a locally running OpenRGB SDK server, and the user's default
  browser opening a documented external URL (`https://anydesk.com/download`) only when the user
  clicks "Download AnyDesk manually". There is no telemetry, analytics, or automatic call-home.
  Full detail: `docs/CODE_SIGNING_POLICY.md`'s privacy statement section.
- The installer includes a documented, working uninstaller ("Apps & features" or the Start Menu
  shortcut) that removes PC Manager and, on request, its per-user settings/logs. It intentionally
  does not remove the separately-installed third-party tools (AnyDesk/OpenRGB/PawnIO), since those
  are independent applications the user may still want - this is disclosed on the uninstall finish
  page.

**Build system:** GitHub Actions, on GitHub-hosted `windows-latest` runners
(`.github/workflows/release.yml`), triggered only by a `vX.Y.Z` tag pushed to this repository after
it has been merged to `main` through a reviewed pull request. The workflow builds from source,
runs the test suite, publishes a self-contained single-file build, compiles the Inno Setup
installer, and (once approved) submits both the published executable and the compiled installer to
SignPath for signing before creating the GitHub Release.

**Maintainer / team:** Single maintainer, [@El1rans](https://github.com/El1rans), who is also the
Committer, Reviewer and Approver for all changes (see `docs/CODE_SIGNING_POLICY.md`). Development
is AI-assisted (changes are drafted with Claude Code as a coding agent), but every change is
submitted as a pull request and reviewed and approved by the maintainer before merge or release.

**Uninstall:** Standard Windows uninstall via "Apps & features", built from the Inno Setup script
(`installer/PCManager.iss`); also available from the Start Menu group.

**Data transfer disclosure:** see the privacy statement and its supporting detail in
`docs/CODE_SIGNING_POLICY.md`.
