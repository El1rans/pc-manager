# Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

Porchlight applies for code signing through the [SignPath Foundation](https://signpath.org/)
program for open source projects. This document is the "code signing policy" SignPath's
application process asks an applicant project to publish: who can commit, review and approve
changes, and what the software does and does not send over the network.

## Status

Signing is being set up. This repository is configured to submit a signing request to SignPath on
every tagged release (see `.github/workflows/release.yml`), but the SignPath project, signing
policy and API token do not exist yet - until SignPath approves the application and the maintainer
completes the one-time setup in `docs/RELEASING.md`, the signing step is skipped automatically and
releases are published unsigned, same as before. Once approved, released installers and the signed
`Porchlight.exe` inside them will carry a certificate issued to SignPath Foundation, and this section
will be updated to say so.

## Team roles

Porchlight currently has one maintainer and no separate review team. For SignPath's roles:

| Role | Who |
|---|---|
| Committers | [@El1rans](https://github.com/El1rans) |
| Reviewers / Approvers | [@El1rans](https://github.com/El1rans) |

Development on this project is AI-assisted (changes are drafted with the help of Claude Code
running as an agent), but every change goes through a pull request with required CI checks and is
reviewed by the maintainer before merge (no formal self-approval exists - GitHub does not let a
maintainer approve their own pull request; merges rely on the required checks passing plus the
maintainer's own review of the diff before clicking merge). No one else can push to `main`, create
a release, or approve a signing request. All team members (currently just the maintainer) use MFA
on both GitHub and SignPath - see `docs/RELEASING.md`'s "Code signing (SignPath)" section.

## Privacy statement

> This program will not transfer any information to other networked systems unless specifically
> requested by the user or the person installing or operating it.

To make that concrete, here is precisely what Porchlight and its installer contact, and when. This
was verified by searching the source tree for every place the app starts a process or opens a
network connection (`Process.Start`/`ProcessStartInfo`, `Socket`/`TcpClient`, and any
`HttpClient`/`WebClient` usage) as of this document's last update:

- **The installer, and Porchlight's own "Set up optional features" page**, run `winget install`
  for each optional component the user explicitly ticks (AnyDesk, OpenRGB, the PawnIO driver).
  `winget` is Microsoft's own package manager; it downloads installers from Microsoft's/the
  package publisher's sources over the internet. Porchlight does not contact any server itself for
  this - it only launches `winget.exe` as a child process (see
  `src/Porchlight.Core/Components/ComponentService.cs`,
  `src/Porchlight.Core/Processes/ProcessRunner.cs`, and the `[Code]` section of
  `installer/Porchlight.iss`).
- **Checking for and installing app updates** (the Updates page) also runs `winget upgrade` /
  `winget install` per selected app - again Microsoft's own winget, started as a child process,
  never contacted directly by Porchlight's own code.
- **On every app start** (unless the user turns off "Check for updates when Porchlight starts" on
  the Updates page - on by default), Porchlight runs `winget upgrade --accept-source-agreements` in
  the background to list available app updates (see `UpdatesAutoCheckHostedService`, registered in
  `UpdatesFeature.cs`). This is the same `winget` child process described above - Porchlight itself
  makes no network call - but `winget` contacts the winget package sources operated by Microsoft
  to list what is available, without further action from the user. See Microsoft's own
  [privacy statement](https://privacy.microsoft.com/privacystatement) for what Microsoft's winget
  service receives. This can be turned off entirely via the toggle above.
- **"Get help" (remote support)**: Porchlight starts the locally installed AnyDesk process
  (`Process.Start`) to read its assigned address and to launch it. Porchlight never sends that
  AnyDesk address, or any other data, to any server of its own or of ours - the address is only
  shown on screen and copied to the clipboard at the user's request, for the user to share
  themselves (e.g. by reading it aloud or messaging it). The one exception is the "Download
  AnyDesk manually" link on that page, which opens `https://anydesk.com/download` in the user's
  default browser only when the user clicks it (`UrlLauncher.Open`, used from
  `RemoteSupportViewModel.DownloadAnyDeskManually`). Whenever AnyDesk itself is running (whether
  started by Porchlight or independently), AnyDesk connects to AnyDesk's own network to provide
  remote-support relaying - that traffic is AnyDesk's, not Porchlight's; see
  [AnyDesk's privacy policy](https://anydesk.com/en/privacy).
- **Lighting (RGB control)**: Porchlight talks to a locally installed and running OpenRGB SDK
  server over a plain TCP socket, by default to `127.0.0.1` (loopback - the default `OpenRgbHost` in
  `LightingSettings.cs`, implemented in the vendored `src/ThirdParty/OpenRGB.NET/`, MIT licensed).
  This host is configurable in `settings.json` (`Lighting.OpenRgbHost`); as long as it is left at
  its default it never leaves the local machine.
- **No telemetry, crash reporting, or analytics.** Porchlight does not call home to any
  Porchlight-operated server, and runs no background network call other than the startup/Updates-page
  winget check described above (both toggleable/user-initiated). Logs stay on disk under
  `%APPDATA%\Porchlight\logs` and are never uploaded anywhere by the app.

If a future change adds a new network call, this section must be updated in the same pull request
that adds it - this file is expected to stay accurate, not aspirational.

## Uninstaller signing limitation

Inno Setup's compiled installer embeds its own uninstaller (`unins000.exe`) inside the installed
application's directory; it is not a separate build output that CI produces or that SignPath's
GitHub Actions-based signing flow can sign. Signing it would require Inno Setup's `SignTool`
directive, which needs a *local* signing tool (e.g. `signtool.exe` with a certificate, or a custom
signing command) available at compile time on the machine running `ISCC.exe` - something SignPath's
cloud/CI-triggered signing flow does not provide, since the certificate's private key never leaves
SignPath's infrastructure.

Practical effect: the installed uninstaller, launched from "Apps & features" or
`{group}\Uninstall Porchlight`, is unsigned even once the installer and `Porchlight.exe` itself are
signed. This is a known, accepted limitation - see also `docs/RELEASING.md`. It has limited impact
in practice: the uninstaller has no Mark-of-the-Web (it was never downloaded directly - it was
extracted to disk by the already-approved, signed installer), so it does not trigger a SmartScreen
"unrecognized app" warning on its own the way a freshly downloaded unsigned `.exe` does.

## Related documents

- `docs/signing/APPLICATION.md` - draft answers for the SignPath Foundation application form.
- `docs/signing/app.xml` / `docs/signing/installer.xml` - the SignPath artifact configurations
  referenced from the signing policy and used by `.github/workflows/release.yml`.
- `docs/RELEASING.md` - the one-time SignPath project setup and the per-release approval step.
- `SECURITY.md` - how to report a vulnerability.
