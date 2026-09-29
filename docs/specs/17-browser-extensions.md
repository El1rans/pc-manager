# 17 - Browser add-ons (branch `feat/browser-extensions`)

A read-only "Browser add-ons" page that lists the extensions installed in Edge, Chrome, Brave and
Firefox and explains, in plain language, which ones deserve a second look. Malicious or
over-powerful browser add-ons are a common way for elderly users' browsers to be hijacked (ads,
redirected searches, stolen logins), and a family member maintaining the PC remotely has no easy
way to see them. Porchlight only shows what is there; removing an add-on is done by the user in the
browser itself.

## Goals

- Enumerate the installed extensions of Edge, Chrome, Brave (every profile) and Firefox (every
  profile), with: browser, profile name, name, enabled or not, installed date, source (official
  store / installed from outside the store / installed by a policy / developer-loaded), and a
  plain-language risk assessment.
- A pure, unit-tested `ExtensionRiskAssessor` that turns an extension's permissions, source, state
  and age into a list of plain-language flags and one overall level: **Looks fine**, **Review**,
  **Worth removing**.
- An action per browser to open that browser's own add-ons page, and a short "How to remove an
  add-on" help text.
- Works fully offline; nothing leaves the PC.

## Non-goals

- **Porchlight never modifies, disables, or deletes browser files, extensions, or preferences.**
  This feature is strictly read-only, and the page says so. (Editing a browser's `Secure
  Preferences` also fails its integrity check and can reset the browser's settings.)
- No malware verdict. The page never says an add-on IS malware; it says what an add-on *can do*
  and suggests removing ones the user does not recognise.
- No store-reputation lookups, no network calls, no extension-id blocklists.
- No other browsers (Opera, Vivaldi, ...) and no per-extension "why is it flagged" deep dive.
- No scanning of browser themes, dictionaries or language packs.

## Design

### Core (`src/Porchlight.Core/Browsers/`, no WPF)

| Type | Role |
|---|---|
| `BrowserKind` | `Edge`, `Chrome`, `Brave`, `Firefox` |
| `ExtensionSource` | `Store`, `Sideloaded`, `Policy`, `Developer`, `Unknown` |
| `InstalledExtension` | Raw facts read from disk: browser, profile name, id, name, description, version, enabled, installed (UTC, nullable), source, permissions, host permissions |
| `ExtensionRiskLevel` | `LooksFine`, `Review`, `WorthRemoving` |
| `ExtensionRiskFlagKind`, `ExtensionRiskFlag` | One plain-language finding (kind, short title, one-sentence explanation) |
| `ExtensionRiskAssessment` | Overall level plus flags |
| `ExtensionRiskAssessor` | Pure static `Assess(InstalledExtension, DateTimeOffset now)` |
| `AssessedExtension` | An `InstalledExtension` plus its assessment |
| `BrowserScanResult` | Extensions found, browsers detected, count of files skipped as unreadable |
| `IBrowserLocations` / `BrowserLocations` | Where the browsers keep their profiles (`%LOCALAPPDATA%` / `%APPDATA%` based) |
| `IBrowserExtensionScanner` / `BrowserExtensionScanner` | Reads all profiles off the UI thread and assesses each extension |
| `ChromiumExtensionReader`, `FirefoxExtensionReader` | Per-engine file parsing used by the scanner |
| `IBrowserExecutableLocator` / `BrowserExecutableLocator` | Finds the browser exe through the `App Paths` registry key |
| `IBrowserAddOnsOpener` / `BrowserAddOnsOpener` | Starts the exe via `IProcessRunner.StartDetached` with the add-ons URL |
| `Demo/DemoBrowserExtensionScanner` | DEBUG-only fake list (`PORCHLIGHT_DEMO_DATA=1`) |

**Chromium (Edge `Microsoft\Edge`, Chrome `Google\Chrome`, Brave `BraveSoftware\Brave-Browser`,
each under `%LOCALAPPDATA%\<vendor>\User Data`).** Every sub-folder holding a `Preferences` file is
a profile (the "System Profile" and "Guest Profile" folders are ignored). The profile's display
name comes from `Local State` -> `profile.info_cache.<folder>.name`, falling back to the folder
name. `extensions.settings` is read from both `Secure Preferences` and `Preferences` and merged by
extension id. Per entry:
- `location` (Chromium `ManifestLocation`): 5 (component) and 10 (external component) are skipped
  as built-in; 1 = store/internal; 2, 3, 6 = external (installed by another program, sideloaded);
  7, 9 = policy; 4, 8 = unpacked / command line (developer). Entries flagged
  `was_installed_by_default` (Chrome's hidden default apps) are skipped, as are leftover entries
  whose extension folder and embedded manifest are both missing.
- Enabled = `state` is not 0 and there are no `disable_reasons`.
- `from_webstore` and `install_time` (WebKit microseconds since 1601) feed source and date.
- The manifest is `Extensions\<id>\<version>\manifest.json` (from the entry's relative `path`, or
  the newest version folder; an absolute `path` for unpacked ones). `name`/`description` starting
  with `__MSG_x__` are resolved via `_locales\<default_locale>\messages.json` (then `en`, `en_US`).
  Permissions come from `permissions` and `host_permissions`; `content_scripts[].matches` count as
  host permissions too.

**Firefox** (`%APPDATA%\Mozilla\Firefox\Profiles\*\extensions.json`). Only `type: "extension"`
entries that are not `hidden` and whose `location` is not a built-in/system location
(`app-builtin*`, `app-system-addons`, `app-system-defaults`) are shown. Enabled = `active`. Source:
`app-temporary` = Developer; `app-global`/`app-system-share` = Sideloaded; an
`installTelemetryInfo.source` of `enterprise-policy` = Policy; otherwise `signedState` 2 = Store
(signed by Mozilla), else Sideloaded. Permissions come from `userPermissions.permissions` and
`.origins`. The profile name is the folder name without its random prefix (`abcd1234.default-release`
-> `default-release`).

**Safe reading.** Browsers keep these files open, so files are opened read-only with
`FileShare.ReadWrite | FileShare.Delete`, and never written. Each file is capped (`Preferences`
and `Secure Preferences` 32 MB, `extensions.json` 16 MB, `manifest.json`/`messages.json` 2 MB;
larger files are skipped and logged). Malformed JSON or an unreadable file skips that file (or that
one extension), is logged with the path, and counts toward `BrowserScanResult.SkippedCount`; it
never fails the scan. Extension names, descriptions and permissions are never logged - only ids,
profile folder names and counts.

### Risk assessment

Flags (title -> what it means):

| Flag | Trigger | Explanation (short) |
|---|---|---|
| Can read and change all websites you visit | host permission `<all_urls>`, `*://*/*`, `http://*/*`, `https://*/*` (or `*://*/`) | It can see what you type and view on every page, including banking sites. |
| Can see your browsing history | `history`, `tabs`, `webNavigation` | It can see which sites you open. |
| Can change downloads | `downloads` | It can start or change file downloads. |
| Can talk to programs on this PC | `nativeMessaging` | It can exchange information with other programs installed on this PC. |
| Can change proxy settings | `proxy` | It can route your web traffic through another computer. |
| Can debug pages | `debugger` | It can inspect and change any page in detail. |
| Can manage other add-ons | `management` | It can turn other add-ons on or off. |
| Not from the official store | source is Sideloaded, Developer or Unknown | It was added some other way than the browser's store, so nobody checked it. |
| Installed by a policy | source is Policy | Businesses use policies on purpose, but so do unwanted programs that force an add-on onto a home PC. If nobody set this up for you, remove it. |
| Installed in the last 7 days | installed within 7 days of now | If you didn't add it, it's worth finding out where it came from. |

Overall level, evaluated in this order:
1. **Worth removing**: the add-on is enabled, is not from the official store (source not `Store`),
   and has at least one *powerful* flag - all-sites access, history, proxy, native messaging,
   debugger or manage-add-ons.
2. **Review**: any flag other than the two "notes" (downloads, installed recently), or the
   combination all-sites access plus proxy.
3. **Looks fine**: otherwise (including "notes" only).

A disabled add-on can never be **Worth removing** (capped at Review) because it is not active. All
wording is calm: "can", never "does"; the level names are only advice ("Worth removing" is shown as
"Worth removing if you don't recognise it").

### App (`src/Porchlight.App/Features/Browsers/`)

`BrowserExtensionsViewModel` (nav `Order` = 8, title "Browser add-ons"), `BrowserExtensionsView`,
a feature registration `AddBrowsersFeature()` (one line in `App.xaml.cs`). The page:
- Header sentence with counts ("We found 14 add-ons in 3 browsers. 2 are worth a look."), a note
  "Porchlight only looks. It never changes or removes anything in your browsers.", and Refresh.
- One section per detected browser, each with "Open <browser>'s add-ons page" and its extensions
  (sorted worst risk first, then name). Each row: status text + icon (never color alone), name,
  profile, enabled, installed date, source, and its flags with the one-sentence explanation.
- A "Show only add-ons to review" toggle.
- "How to remove an add-on": open the add-ons page, find the add-on, choose Remove (Firefox: the
  ... menu -> Remove), confirm; ask a family member if unsure.
- Empty state ("No browser add-ons found") and an error state; the scan runs off the UI thread on
  navigation and on Refresh, cancellable.
- If the browser exe cannot be found: "We couldn't open Edge automatically. Open it, then type
  edge://extensions in the address bar."
- DEBUG demo mode (`PORCHLIGHT_DEMO_DATA=1`) swaps in a fake scanner with a made-up list.

## Privacy and safety

Nothing is sent anywhere; no network access. Data stays in memory for the page's lifetime and is
not persisted (no settings section). Logs contain extension ids, profile folder names and counts
only. Files are only ever opened for reading.

## Acceptance criteria

1. Edge, Chrome, Brave (all profiles, names from `Local State`) and Firefox extensions are listed;
   built-in/component/default-app entries are not.
2. `__MSG_x__` names and descriptions are resolved from `_locales`.
3. Files held open by a running browser are read without error; oversized or malformed files are
   skipped and logged, and the rest of the scan still succeeds.
4. Every extension shows enabled state, installed date (when known), source, risk level (text +
   icon) and its flags with plain explanations.
5. `ExtensionRiskAssessor` maps each flag trigger and each overall-level rule above, with unit
   tests; wording never claims malware.
6. "Open this browser's add-ons page" starts the browser via `IProcessRunner.StartDetached` with
   the right URL; a missing browser gives a friendly fallback message.
7. The page states that Porchlight never changes browsers; no code path writes to browser files.
8. Tests use fixture files in a temp directory - never the real machine's profiles.
9. `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes; README "Features"
   updated.
