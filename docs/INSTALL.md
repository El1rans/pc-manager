# Installing Porchlight

The short version is in the [README](../README.md#install). This page has the details.

Download `Porchlight-Setup-<version>.exe` from the [Releases](https://github.com/El1rans/porchlight/releases) page and run it.

- The installer is currently unsigned (see "Code signing policy" below), so Windows SmartScreen will warn
  that it "prevented an unrecognized app from starting". Click **More info**, then **Run anyway**
  to continue - this is expected for every release until SignPath approves code signing for this
  project.
- Setup asks for administrator rights (needed for a per-machine install and, if you choose the
  PawnIO driver below, its kernel driver), then lets you pick which optional components to set
  up. Each is installed with `winget`, so it needs an internet connection the first time:
  - **Remote help from family (AnyDesk)** - ticked by default. Lets a family member connect to
    help you, from the "Get help" page.
  - **RGB lighting control (OpenRGB)** - unticked by default. Needed for the Lighting page to
    control RGB devices (motherboard, RAM, GPU, keyboard, ...).
  - **Fan control and temperature sensors (PawnIO driver)** - unticked by default. Installs a
    signed kernel driver Porchlight's Hardware page needs for full sensor access and software fan
    control.
  - If `winget` is not available on your PC, Setup skips these and tells you so - Porchlight's own
    Settings > Optional features (in the sidebar) can install them later.
  - You can also choose to create a desktop shortcut and/or start Porchlight when anyone signs in
    to this PC.
- Uninstalling Porchlight (Windows Settings > Apps) does **not** remove AnyDesk, OpenRGB or the
  PawnIO driver - they are separate applications; uninstall them individually if you no longer
  need them. You will be asked whether to also delete Porchlight's settings and logs.
- For an unattended install, run the setup exe with `/VERYSILENT` and, to control which optional
  components are installed, `/COMPONENTS="anydesk,openrgb,pawnio"` (comma-separated ids from
  `installer/Porchlight.iss`'s `[Components]` section; omit ids you don't want installed, or pass
  `/COMPONENTS=""` to install none of them).
- **Upgrading from PC Manager?** Porchlight is the renamed version of the same app (same install,
  same data, nothing to redo). Installing over an existing PC Manager install moves it to
  `Program Files\Porchlight`, and your settings are migrated automatically from
  `%APPDATA%\PCManager` to `%APPDATA%\Porchlight` the first time Porchlight runs - the old folder
  is left in place, untouched.

See [`RELEASING.md`](RELEASING.md) for how a new release is cut, and [`specs/07-installer.md`](specs/07-installer.md) for the
installer's full design.

### Code signing policy

Porchlight has applied for free code signing through the
[SignPath Foundation](https://signpath.org/) program for open source projects. Signing is being
set up: `.github/workflows/release.yml` is wired to submit each tagged release for signing, but
releases are signed only once SignPath approves the application and the maintainer finishes the
one-time project setup - see [`CODE_SIGNING_POLICY.md`](CODE_SIGNING_POLICY.md) for the
full policy (team roles, privacy statement, what Porchlight contacts over the network) and
[`RELEASING.md`](RELEASING.md) for the setup steps. Until then, every release stays unsigned and SmartScreen
will warn as described above.

Free code signing provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

## Updating

An installed copy of Porchlight checks GitHub Releases (at startup unless the "Check for updates when Porchlight starts" option is off, and on Refresh) and shows "Porchlight X.Y.Z is available" at the top of the Tune-up > Updates page with a "What's new" link and an "Update now" button. It downloads the installer, verifies its SHA-256, runs it (one Windows permission prompt) and restarts Porchlight on the new version. A copy that wasn't installed with the installer only gets a link to the release page. See [`specs/23-self-update.md`](specs/23-self-update.md).
