# 07 - Installer and releases (branch `chore/installer`)

Runs after the feature milestones.

Goal: a single `Porchlight-Setup-<version>.exe` a family member can download and run, which installs Porchlight and optionally its helper components.

## Build

- `dotnet publish src/Porchlight.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` (no .NET install needed on the target PC). Check that WPF + LibreHardwareMonitorLib work single-file; if not, publish as a folder.
- Installer: Inno Setup 6 script `installer/Porchlight.iss` (install Inno Setup in CI with `choco install innosetup` or the official action).
  - Per-machine install to `Program Files\Porchlight`, Start menu shortcut, optional desktop shortcut, uninstaller. `PrivilegesRequired=admin` (needed for PawnIO anyway).
  - **Components page** with checkboxes (defaults: all unticked except as noted):
    - "Remote help from family (AnyDesk)" - ticked by default.
    - "RGB lighting control (OpenRGB)".
    - "Fan control and temperature sensors (PawnIO driver)" - with a short note that it installs a signed hardware driver.
  - For each ticked component, `[Run]` executes `winget install --id <id> --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity` with a status message. If winget is missing, skip with a message; Porchlight's first-run setup can install them later.
  - Writes a marker so Porchlight's first-run setup pre-checks what the installer already handled (it re-detects anyway) - see "Contract with first-run setup" below.
  - Optional "Start Porchlight when anyone signs in to this PC" task. Implemented as a
    `{commonstartup}` (Common Startup folder) shortcut, not a current-user HKCU Run key: this is a
    per-machine install, so a per-user Run key or Startup-folder entry for just the installing
    user would be wrong on a shared family PC. A Common Startup shortcut starts Porchlight for
    every account that signs in, still shows up in Task Manager's Startup tab (an HKLM Run key
    would not), and is removed automatically by the uninstaller like any other shortcut.
  - Uninstall does NOT remove AnyDesk/OpenRGB/PawnIO (they are separate apps; say so on the finish page) and deletes `%APPDATA%\Porchlight` only if the user agrees.
- Versioning: `Version` in `Directory.Build.props`; tag `vX.Y.Z` triggers `.github/workflows/release.yml`: build, test, publish, compile installer, create a GitHub Release with the setup exe and SHA-256 checksum. CHANGELOG section becomes the release notes.
- Code signing is out of scope for now (document that SmartScreen will warn for unsigned installers).

## Contract with first-run setup

Defined now (01b) so both sides can build against it without either one waiting on the other.

- After running `winget install` for a ticked component, the installer writes
  `HKLM\Software\Porchlight\Installer`, value `Components` (`REG_SZ`), to a comma-separated list of
  the component ids (see `Porchlight.Core.Components.ComponentIds`: `anydesk`, `openrgb`, `pawnio`)
  it just ran winget for - regardless of whether that winget call actually succeeded.
- The value is additive/idempotent: if it already exists (e.g. a repair install), the installer
  merges its own ids into the existing comma-separated list rather than overwriting it.
- Porchlight's first-run setup (`SetupViewModel.LoadAsync`, via
  `IRegistryReader.GetInstallerHandledComponentIds()`) reads this value once, but **always
  re-detects every component itself** via `IComponentService.GetStatusAsync` - the marker only
  affects which not-yet-detected-as-installed items default to ticked (an id in the list defaults
  to unticked, since the installer already attempted it), never a component's reported status.
- The installer does not need to delete or update this value on uninstall; a stale entry only
  means first-run setup defaults that item to unticked, which is harmless (the user can still tick
  it).

## Acceptance criteria

- [ ] Fresh Windows VM/PC: setup installs Porchlight and ticked components; app launches from Start menu.
- [ ] Uninstall removes Porchlight cleanly.
- [ ] Pushing a tag produces a GitHub Release with the installer.
