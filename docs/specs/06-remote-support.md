# 06 - Remote support with AnyDesk (branch `feat/remote-support`)

> **Amended after 01b:** AnyDesk is the `anydesk` component from milestone 01b. Detection, install and start go through `IComponentService`; the "Not installed" state uses the shared `ComponentCard` (styled larger on this page). `IAnyDeskService` adds only ID/alias reading and formatting on top.

Goal: a "Get help" page a non-technical user (e.g. a parent) can open to get remote support in two clicks. The helper (family member) connects with AnyDesk using the address shown on the page.

AnyDesk is proprietary and cannot be embedded. Porchlight installs it through winget, reads its address, and launches it. It never changes AnyDesk security settings on its own.

## Core (`Porchlight.Core/RemoteSupport`)

- `IAnyDeskService`:
  - `GetStateAsync(ct)` -> `AnyDeskState(IsInstalled, ExePath?, Version?, Id?, Alias?, IsRunning, ServiceStatus)`.
  - Locate the exe: `%ProgramFiles(x86)%\AnyDesk\AnyDesk.exe`, `%ProgramFiles%\AnyDesk\AnyDesk.exe`, then the uninstall registry entries (`DisplayName` starting with "AnyDesk", read `InstallLocation`/`DisplayIcon`). Portable copies are not supported.
  - Read the ID: first run `AnyDesk.exe --get-id` (capture stdout, stdin redirected and closed, 5 s timeout); if that fails, parse `ad.anynet.id=` from `%ProgramData%\AnyDesk\system.conf` (fallback `service.conf`). Alias from `--get-alias` or `ad.anynet.alias=`. Verify these CLI switches against AnyDesk's official command-line documentation before relying on them.
  - `InstallAsync(IProgress<string> log, ct)`: `winget install --id AnyDesk.AnyDesk --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity`. Reuse the `IWingetClient` process runner from the Updates feature (if not merged yet, depend on the interface and coordinate). After install, poll `GetStateAsync` until an ID appears (up to 60 s; AnyDesk registers its ID on first start, so start it once if needed).
  - `LaunchAsync()`: start AnyDesk normally so incoming requests show the accept dialog.
- `AnyDeskIdFormatter`: formats numeric IDs in groups of three ("123 456 789", "1 234 567 890"); leaves aliases (contain `@` or letters) unchanged. Unit tested.
- Config file parser is a pure function over file text. Unit tested with samples (id present, missing, alias only, Windows line endings, extra spaces).

## App (`Porchlight.App/Features/RemoteSupport`)

Nav item "Get help" (icon: people/headset), placed LAST in the nav, visually distinct.

States:
1. **Not installed**: friendly explanation ("To let someone you trust help you, install AnyDesk.") + big accent "Install AnyDesk" button, progress + log while installing. If winget is missing, show the download link https://anydesk.com/download.
2. **Installed**: 
   - Very large, selectable address text (at least 36pt, grouped digits), "Copy address" button (copies without spaces; shows "Copied" confirmation for 2 s).
   - Status line with icon + text: "AnyDesk is running - ready for a connection" / "AnyDesk is not running" with "Start AnyDesk" button.
   - Plain-language instructions: "1. Call the person helping you. 2. Read them this number. 3. When AnyDesk asks, click Accept only if you are talking to them right now."
   - Safety note (caution style, icon + text): "Never accept a connection from someone who called you unexpectedly. Real companies (banks, Microsoft) will not ask you to install AnyDesk."
   - "Share" row: "Copy support info" copies a short text block (computer name, Windows version, AnyDesk address) for sending by message.
3. **Error**: plain message + Retry.

Settings (`RemoteSupport` section): optional "Helper name" shown on the page ("Your helper: Eliran") - the user sets it; default empty (hidden).

## Explicitly out of scope (security)

- Setting an unattended-access password, enabling unattended access, or changing AnyDesk permissions from Porchlight. These make the PC reachable without the user present; the helper configures them inside AnyDesk if the family wants it.
- Uploading or sending the ID anywhere automatically.

## Tests

ID formatter; config parser; exe locator with a fake file system/registry abstraction; state transitions in the ViewModel with a fake `IAnyDeskService` (not installed -> installing -> installed with ID).

## Acceptance criteria

- [ ] On a PC without AnyDesk: Install works end to end and the address appears without restarting Porchlight.
- [ ] On a PC with AnyDesk: address shown within 2 s of opening the page; Copy puts the digits-only ID on the clipboard.
- [ ] Start AnyDesk button launches it; status updates within a few seconds.
- [ ] No AnyDesk security setting is changed by Porchlight.
