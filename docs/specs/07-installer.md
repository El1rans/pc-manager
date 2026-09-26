# 07 - Installer and releases (branch `chore/installer`)

Runs after the feature milestones.

Goal: a single `PCManager-Setup-<version>.exe` a family member can download and run, which installs PC Manager and optionally its helper components.

## Build

- `dotnet publish src/PCManager.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` (no .NET install needed on the target PC). Check that WPF + LibreHardwareMonitorLib work single-file; if not, publish as a folder.
- Installer: Inno Setup 6 script `installer/PCManager.iss` (install Inno Setup in CI with `choco install innosetup` or the official action).
  - Per-machine install to `Program Files\PC Manager`, Start menu shortcut, optional desktop shortcut, uninstaller. `PrivilegesRequired=admin` (needed for PawnIO anyway).
  - **Components page** with checkboxes (defaults: all unticked except as noted):
    - "Remote help from family (AnyDesk)" - ticked by default.
    - "RGB lighting control (OpenRGB)".
    - "Fan control and temperature sensors (PawnIO driver)" - with a short note that it installs a signed hardware driver.
  - For each ticked component, `[Run]` executes `winget install --id <id> --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity` with a status message. If winget is missing, skip with a message; PC Manager's first-run setup can install them later.
  - Writes a marker so PC Manager's first-run setup pre-checks what the installer already handled (it re-detects anyway).
  - Optional "Start PC Manager when Windows starts" task (current user Run key).
  - Uninstall does NOT remove AnyDesk/OpenRGB/PawnIO (they are separate apps; say so on the finish page) and deletes `%APPDATA%\PCManager` only if the user agrees.
- Versioning: `Version` in `Directory.Build.props`; tag `vX.Y.Z` triggers `.github/workflows/release.yml`: build, test, publish, compile installer, create a GitHub Release with the setup exe and SHA-256 checksum. CHANGELOG section becomes the release notes.
- Code signing is out of scope for now (document that SmartScreen will warn for unsigned installers).

## Acceptance criteria

- [ ] Fresh Windows VM/PC: setup installs PC Manager and ticked components; app launches from Start menu.
- [ ] Uninstall removes PC Manager cleanly.
- [ ] Pushing a tag produces a GitHub Release with the installer.
