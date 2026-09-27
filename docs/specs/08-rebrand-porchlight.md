# 08 - Rebrand to Porchlight (branch `chore/rebrand-porchlight`)

The product is renamed from **Porchlight** to **Porchlight**. Tagline: **"We leave the light on for you."**

## Brand kit (already designed - use as-is, do not redraw)

Delivered in `brand/` (copy into `assets/brand/` at the repo root):

| File | Use |
|---|---|
| `porchlight-icon.svg` | Master vector icon (64-unit grid, radial glow clipped to tile) |
| `porchlight-icon-small.svg` | Hand-tuned 16-unit variant used for 16/20/24 px |
| `porchlight.ico` | Windows icon: 16, 20, 24, 32, 40, 48, 64, 128, 256 (PNG-compressed entries; small sizes from the small variant) |
| `porchlight-logo-light.svg` / `.png`, `porchlight-logo-dark.svg` / `.png` | Lockup (icon + wordmark + tagline). PNGs are 1040x256 (2x) |
| `wizard-100.bmp`, `wizard-200.bmp` | Inno Setup `WizardImageFile` (164x314 @1x, 328x628 @2x) |
| `wizard-small-100.bmp`, `wizard-small-200.bmp` | Inno Setup `WizardSmallImageFile` (55 / 110 px) |
| `render-brand-assets.py` | Regenerates the PNG/ICO/BMP files (Python + Pillow, uses Segoe UI from C:\Windows\Fonts) |

Palette: navy `#1E2A44`, amber `#F5B942`, window cream `#FFF6DE`, flame `#E86A17`.

## Rename scope

1. **User-visible name everywhere**: window title, sidebar header (replace the text header with the small icon + "Porchlight"), About/version text, first-run setup window, dialogs and messages ("Porchlight could not start" etc.), tooltips, README, CONTRIBUTING, CHANGELOG (new entry, don't rewrite history), docs, specs (add a note at the top of 00 that the product was renamed; don't rewrite old specs wholesale - update titles and names where they describe the current product).
2. **Code**: rename projects, assemblies, root namespaces and folders `Porchlight.*` -> `Porchlight.*` (`Porchlight.Core`, `Porchlight.App`, `Porchlight.Core.Tests`, `Porchlight.App.Tests`), solution `Porchlight.slnx`, exe `Porchlight.exe`. Use `git mv` so history follows. Update InternalsVisibleTo, csproj references, CI paths, publish paths.
3. **App icon**: `ApplicationIcon` = `assets/brand/porchlight.ico` (or a copy under the App project); window `Icon`; taskbar. Remove the old generated `AppIcon.ico`.
4. **Data locations with migration** (critical - existing installs must keep their settings):
   - New: `%APPDATA%\Porchlight\settings.json`, logs `%APPDATA%\Porchlight\logs`, fan-control marker in the same folder.
   - On startup, if `%APPDATA%\Porchlight` doesn't exist and `%APPDATA%\Porchlight` does: copy settings.json (and the fan-control activity marker if present - the stale-marker banner must still work after migration) to the new folder, log it, leave the old folder in place (don't delete user data). Never overwrite existing new-location files. Unit tests with a temp directory for: fresh install, migrate, both exist, old corrupt file (copied then handled by the normal corrupt-file path), partial failure (logged, app still starts).
   - Registry installer marker: new key `HKLM\Software\Porchlight\Installer`; the app reads the new key and falls back to the legacy `HKLM\Software\Porchlight\Installer`. Update docs/specs/07-installer.md contract.
   - Mutex names: `PorchlightAppMutex` and `Global\PorchlightAppMutex`. The installer's `AppMutex` lists the new AND legacy names (`PorchlightAppMutex,Global\PorchlightAppMutex`) so upgrading over an old running build still prompts to close it.
5. **Installer** (`installer/Porchlight.iss`):
   - `AppName=Porchlight`, `AppPublisher=El1rans`, `AppPublisherURL`/`AppSupportURL` = https://github.com/El1rans/porchlight, output `Porchlight-Setup-<version>.exe`, install dir `{autopf}\Porchlight`, Start menu group/shortcut "Porchlight", `SetupIconFile` = the new ico, `WizardImageFile=wizard-100.bmp,wizard-200.bmp`, `WizardSmallImageFile=wizard-small-100.bmp,wizard-small-200.bmp`, `WizardStyle=modern`.
   - **Keep the same `AppId` GUID** so installing Porchlight over Porchlight is treated as an upgrade (one entry in Apps & features). Set `UsePreviousAppDir=no` so it moves to `Program Files\Porchlight`; in `[InstallDelete]` remove the old `{autopf}\Porchlight\Porchlight.exe` and the old Start menu/desktop/startup shortcuts named "Porchlight". Uninstall's optional data deletion covers `%APPDATA%\Porchlight` and, if present, the legacy `%APPDATA%\Porchlight`.
6. **CI / release**: update paths, artifact names (`Porchlight-Setup-<ver>.exe` + `.sha256`), SignPath artifact configuration docs (`docs/signing/*.xml` file names inside the zip: `Porchlight.exe`), release notes. Keep workflow job names `build-and-test` and `build-installer` unchanged (branch protection requires them).
7. **Repo**: the maintainer's coordinator renames the GitHub repo to `El1rans/porchlight` - you do NOT rename the repo. Use the new URL in docs; GitHub redirects the old one.
8. **README**: header uses `<picture>` with `assets/brand/porchlight-logo-dark.png` for `prefers-color-scheme: dark` and the light PNG otherwise (alt "Porchlight — We leave the light on for you."), then a one-paragraph description aimed at families helping parents.
9. **Screenshots**: retake all `docs/screenshots/*` with the demo-data mechanism (if merged) showing the new name/icon; never real machine data or AnyDesk IDs.

## Out of scope

Local folder `E:\Porchlight` stays as is. No behaviour changes beyond the rename, icon and migration.

## Acceptance criteria

- [ ] `rg -i "pc ?manager|pcmanager"` over the repo returns only: legacy-migration code/tests, the legacy mutex/registry names, CHANGELOG history, and historical spec notes.
- [ ] Build 0 warnings; all tests pass (3 runs); new migration tests.
- [ ] App launches (non-elevated) showing the Porchlight name and icon (window, taskbar, sidebar); settings from `%APPDATA%\Porchlight` are migrated on first run (verify with a backup/restore of the real folder - never lose the user's settings).
- [ ] CI builds `Porchlight-Setup-<version>.exe`; wizard images and icon present (inspect the artifact's resources, don't run it).
