# 23 - Self-update (branch `feat/self-update`)

Until now Porchlight had no way to update itself: the Updates page only upgrades *other* apps through
winget, so a new Porchlight meant downloading and running the installer by hand. This milestone lets an
installed Porchlight find new versions on GitHub Releases and install them from inside the app.

## Goals

- Install once with the installer; afterwards new versions arrive from inside the app in two clicks
  ("Update now", then accept the Windows permission prompt).
- Nothing is run unless it is exactly what the release published: the installer's SHA-256 must match
  the release's `.sha256` file, and it may only be fetched from GitHub's own hosts over https.
- A copy that was not installed by the installer (unzipped, build output) never tries to update
  itself; it just links to the release page.

## Non-goals

- No silent background installs: the user always presses "Update now".
- No pre-release/beta channel (GitHub's "latest release" endpoint never returns those).
- No delta updates, no downgrade, no update of the winget-managed apps (that is the rest of the page).
- Requiring an Authenticode signature is prepared but switched off (releases are unsigned today).

## How it fits together

Releases are produced by `.github/workflows/release.yml`: a GitHub Release titled "Porchlight X.Y.Z" on
tag `vX.Y.Z` with two assets, `Porchlight-Setup-X.Y.Z.exe` and `Porchlight-Setup-X.Y.Z.exe.sha256`
(one line: `<lower-case sha256> *Porchlight-Setup-X.Y.Z.exe`, no trailing newline). The updater depends on
exactly those names and formats - see `docs/RELEASING.md`.

### Core (`src/Porchlight.Core/SelfUpdate/`), all behind interfaces

- `IReleaseChecker` / `GitHubReleaseChecker`: `GET https://api.github.com/repos/El1rans/porchlight/releases/latest`,
  `User-Agent: Porchlight/<version>`, `Accept: application/vnd.github+json`, 10 s timeout. Offline,
  rate-limited (403/429), server errors and unusable replies all become
  `SelfUpdateCheckStatus.CouldNotCheck`, logged at Information/Warning, never thrown or shown. A 404 (no
  release yet) means up to date.
- `ReleaseParser`: reads `tag_name` (strict `vX.Y.Z`; anything else is ignored, as are drafts and
  pre-releases), `html_url` (only if an https github.com URL, else the repository's tag page), `body`
  and the two assets by exact name. Versions are compared with `System.Version` after padding missing
  components with 0 (so `1.2` equals `1.2.0`).
- `IInstallTypeDetector` / `InstallTypeDetector`: "installed" when the running exe's folder equals the
  `InstallLocation` in `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A9E642BD-...}_is1`
  (64-bit view; case-insensitive, after normalising both paths); otherwise "portable". The registry read
  is behind `IInstallLocationReader`.
- `IUpdateDownloader` / `UpdateDownloader`: clears then fills `%LOCALAPPDATA%\Porchlight\Updates`,
  streams the installer to a `.download` file while hashing it, reports progress
  (`Downloading` with a fraction, then `Verifying`), and only renames it to its final name when the hash
  matches. Both URLs must pass `SelfUpdateUrlPolicy` (https, default port, no credentials, host
  `github.com`, `objects.githubusercontent.com` or `release-assets.githubusercontent.com`) before the first
  request and again on every redirect hop (redirects are followed by hand; the handler must not follow
  them). Any failure deletes the file and throws `SelfUpdateException` with a plain-language message.
- `SelfUpdatePolicy.RequireSignedInstaller` (a `const`, `false` today): when `true`, the downloader also
  requires `IInstallerSignatureVerifier` (`WinVerifyTrust`, no revocation lookup) to accept the installer
  before it is run. Flipping it to `true` is the whole change needed once every release is signed - do
  not do that before the first signed release exists, or every update would fail.
- `IInstallerLauncher` / `InstallerLauncher`: starts the installer with
  `/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1` via ShellExecute (so a non-elevated Porchlight gets
  one UAC prompt). Declining it (Win32 error 1223) is reported as `Declined`, not as a failure.

### Installer (`installer/Porchlight.iss`)

- `InitializeSetup`: when `/RELAUNCH=1` is passed, polls (every 250 ms, up to 30 s) until both
  Porchlight mutexes (`PorchlightAppMutex`, `Global\PorchlightAppMutex`) are gone before continuing, so
  Porchlight's shutdown can't make a silent Setup abort on `AppMutex`.
- A second `[Run]` entry starts `{app}\Porchlight.exe` (`nowait`, `skipifnotsilent`,
  `Check: ShouldRelaunchAfterSilentInstall`) after a silent, `/RELAUNCH=1` install. No
  `runasoriginaluser`, so an elevated Porchlight stays elevated. The existing interactive
  "Launch Porchlight" checkbox is unchanged.

### App (`src/Porchlight.App/Features/Updates/`)

- `PorchlightUpdateViewModel` owns the card and the flow: check -> (Update now) download -> verify ->
  launch installer -> `IAppLifetime.Shutdown()` (the same exit path as the tray's Exit, so fans are
  handed back and the mutexes are released). `UpdatesViewModel.PorchlightUpdate` exposes it.
- When to check: at startup only if "Check for updates when Porchlight starts" is on
  (`UpdatesAutoCheckHostedService`, in the background, never delaying the window), and whenever the
  Updates page's Refresh is pressed. Concurrent checks share one request. There is no periodic timer.
- The Updates nav badge counts the winget updates plus one when a newer Porchlight exists. The tray
  "app updates ready" alert and the check-up report still count winget updates only.

## UI

A card above the Updates page's toolbar, visible only when a newer version exists:

- "Porchlight X.Y.Z is available - you have A.B.C" and a "What's new" link (opens the release page).
- Installed copy: an "Update now" button; while working, a progress bar with "Downloading... 42%",
  "Verifying...", "Starting installer..." and a Cancel button; failures appear as a red line under it.
- Portable copy (or a release with no installer attached): "Automatic updates need the installed version
  of Porchlight. Download the new version from the release page." and a "Download" link.

## Privacy

`docs/CODE_SIGNING_POLICY.md`'s privacy statement lists the new contacts: a plain GET to
`api.github.com` with a `User-Agent` of `Porchlight/<version>` at startup (if the toggle is on) or on
request, and a download from GitHub only after the user presses "Update now". Turn off the startup check
with the existing toggle.

## Acceptance criteria

- [ ] A newer release makes the card appear with the right versions, and adds 1 to the Updates badge
      (and to the Tune-up category badge); no newer release, a failed check or being offline shows nothing.
- [ ] Installed copy: "Update now" downloads, verifies, starts the installer, and Porchlight exits;
      after Setup finishes the new version starts by itself (elevated if the old one was).
- [ ] Portable copy: no "Update now"; the explanation and a "Download" link to the release page.
- [ ] A hash mismatch, malformed checksum file, disallowed host (initial or after a redirect), HTTP error
      or network failure deletes the download and shows a friendly message; nothing is run.
- [ ] Declining the UAC prompt keeps Porchlight running and shows a message; pressing "Update now" again works.
- [ ] Cancel during the download stops it and removes the partial file.
- [ ] With the startup toggle off, no request to GitHub is made at startup; Refresh still checks.
- [ ] `installer/Porchlight.iss` interactive behaviour is unchanged (no `/RELAUNCH`): normal mutex prompt,
      "Launch Porchlight" checkbox.
- [ ] Unit tests (no real network): version comparison, release JSON parsing (missing assets, malformed
      tag, pre-release), URL allow-list, SHA-256 match/mismatch/bad checksum format, redirects, install-type
      detection, check failures, view-model flow (success, download failure, UAC declined, portable), badge.
- [ ] Privacy statement, README, CHANGELOG and `docs/RELEASING.md` updated; `dotnet build -c Release`
      0 warnings; tests pass.
- [ ] Manual, needs two real releases: install version N with the installer, publish N+1, and update
      from inside the app.
