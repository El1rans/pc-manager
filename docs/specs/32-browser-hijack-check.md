# 32 - Browser hijack check (branch `feat/browser-hijack-check`)

Unwanted programs often take over a browser's home page, start-up pages, new-tab page or default
search engine so that every search goes through their ad site. A family member looking after the PC
remotely has no easy way to see this. This milestone extends the "Browser add-ons" page
(spec 17) with a read-only "Start-up and search settings" check per browser.

## Goals

- For Chrome, Edge and Brave (every profile) and Firefox (every profile), read the **home page**,
  the **pages that open at start-up**, the **new-tab page** (where it can be set outside an
  add-on) and the **default search engine**, and show for each one a plain verdict: **Looks fine**,
  **Changed**, or **Forced by a setting on this PC**.
- Also read the browser **policies** in the registry (`HKLM` and `HKCU`, `Software\Policies\Google\Chrome`,
  `...\Microsoft\Edge`, `...\BraveSoftware\Brave`): `HomepageLocation`,
  `DefaultSearchProviderSearchURL`, `RestoreOnStartupURLs`, `NewTabPageLocation`. A policy that points
  somewhere unfamiliar on a home PC is a strong hijack sign and gets the stronger wording.
- A pure, unit-tested `BrowserHijackClassifier`: a known-good list of providers and home pages
  (Google, Bing, DuckDuckGo, Yahoo, Ecosia, Brave, Startpage, msn.com, Microsoft, Mozilla, the
  browser's own pages, empty = browser default) is **Looks fine**; anything else is **Changed** and
  shows the plain host name; an unfamiliar address that a policy forces is **Forced by a setting on
  this PC**.
- Show the results as a block at the top of each browser's card on the Browser add-ons page, with
  plain advice ("If you didn't choose this, reset it in the browser's settings") and a button that
  opens the browser's own settings page for that setting.
- Works offline; nothing leaves the PC. DEBUG demo-mode fake.

## Non-goals

- **Read-only.** Porchlight never changes browser files, preferences or policies, and never resets a
  setting for the user (editing `Secure Preferences` also fails Chromium's integrity check). The user
  resets it in the browser, or the family helper removes the policy.
- No malware verdict: an unfamiliar address is not proof of anything, and the wording says "changed",
  not "infected". The known-good list is advice, not an allow/deny list.
- No network lookups, no reputation checks.
- **Firefox policies are not read** (`policies.json` next to the install and the rarely used
  `Software\Policies\Mozilla\Firefox` key). A Firefox that is forced by a policy still shows the
  profile's own values.
- **Firefox new-tab page** is only checked through the legacy `browser.newtab.url` preference;
  modern Firefox has no setting for it outside add-ons/policies.
- **Chromium new-tab page** is only checked through the `NewTabPageLocation` policy; a new-tab page
  taken over by an *extension* (`chrome_url_overrides`) is already covered by the add-on risk list.
- Chromium search engine is read from `default_search_provider_data`; a profile that never changed
  it has no such entry and is shown as "Browser default".

## Design

### Core (`src/Porchlight.Core/Browsers/`, no WPF)

| Type | Role |
|---|---|
| `HijackSetting` | `HomePage`, `StartupPages`, `NewTabPage`, `SearchEngine` |
| `HijackStatus` | `Ok`, `Changed`, `ForcedByPolicy` |
| `HijackFinding` | Browser, profile (null = browser-wide policy), setting, status, plain value to show |
| `BrowserHijackResult` | Findings, browsers checked, skipped-file count |
| `BrowserHijackClassifier` | Pure `Classify(address, forcedByPolicy)` and `ClassifyAll(addresses, forcedByPolicy)` |
| `IBrowserPolicyReader` / `BrowserPolicyReader` | Reads the Chromium policy keys (read-only registry access) into `BrowserPolicySettings` |
| `IBrowserHijackScanner` / `BrowserHijackScanner` | Reads all profiles off the UI thread, merges policies, returns findings |
| `ChromiumSettingsReader`, `FirefoxSettingsReader`, `MozLz4Decoder` (internal) | Per-engine file parsing |
| `Demo/DemoBrowserHijackScanner` | DEBUG-only fake findings (`PORCHLIGHT_DEMO_DATA=1`) |

`IBrowserAddOnsOpener` gains `TryOpenSettings(kind, setting)`; the page URLs are
`chrome://settings/search` / `onStartup`, `edge://settings/search` / `startHomeNTP`,
`brave://settings/search` / `getStarted`, `about:preferences#search` / `#home`. Like the add-ons
button, it starts the browser exe (found through `App Paths`) with the URL as its argument via
`IProcessRunner.StartDetached`; if the browser cannot be found the page shows "We couldn't open X
automatically. Open it, then type <url> in the address bar."

**Classification rules** (`Classify`):
1. Empty or blank: **Looks fine**, "Browser default".
2. `{google:baseURL}...` (Chromium's own Google placeholder): Google, fine.
3. The browser's own schemes (`chrome:`, `edge:`, `brave:`, `about:`, ...): fine, "The browser's own page".
4. `file:` -> Changed, "A file on this PC". Other schemes (`javascript:`, ...) -> Changed, "An unusual
   address". Text that is not an address -> Changed, truncated.
5. `http(s)` host (lower-case, without `www.`): fine when its registrable name is one of the known names
   (`google`, `bing`, `duckduckgo`, `yahoo`, `ecosia`, `brave`, `startpage`, `msn`, `microsoft`,
   `mozilla`) directly before the TLD, or before a `co.`/`com.`/`org.`... country second level. Look-alikes
   (`google.evil.com`, `evil-google.com`, `google.com.evil.ru`) are not fine.
6. Otherwise Changed (or Forced by a setting on this PC when `forcedByPolicy`). A known-good address that a
   policy forces stays fine.

For several addresses (start-up pages) the worst verdict wins and up to three distinct hosts are listed.

**Chromium** (`Preferences` and `Secure Preferences` per profile; `Secure Preferences` wins; profile
names from `Local State`, same profile rules as spec 17): `homepage` (ignored when
`homepage_is_newtabpage` is true), `session.restore_on_startup` (only `4` = open specific pages
makes `session.startup_urls` count; `5` = new-tab page, `1` = reopen last tabs are fine) and
`default_search_provider_data.template_url_data.url`.

**Firefox** (per profile): `prefs.js` then `user.js` (which wins) `user_pref("name", value);` lines for
`browser.startup.homepage` (several pages separated by `|`) and `browser.newtab.url`. The default
search engine comes from `search.json.mozlz4`: an 8-byte magic, a 4-byte size and one LZ4 block, which
`MozLz4Decoder` expands (about 80 lines; bounds-checked, 8 MB output cap, a corrupt file throws and is
skipped). The engine named by `metaData.defaultEngineId` (or the older `metaData.current`) is looked up in
`engines[]` and its `_urls[0].template` classified; an engine Firefox ships itself (`_isAppProvided`) is
always fine. No default engine recorded means the browser default.

**Policies override profiles.** For a browser with a policy value for a setting, only the browser-wide
policy finding is reported for that setting (Chromium ignores the profile's own value anyway).

**Safe reading.** Same rules as spec 17: files opened with `FileShare.ReadWrite | FileShare.Delete`,
never written, size-capped (Chromium JSON 32 MB, `prefs.js`/`user.js`/`search.json.mozlz4` 8 MB),
malformed or unreadable files skipped, logged with the path and counted, never failing the scan. Logs
contain no addresses, only profile folder names and counts.

### App (`src/Porchlight.App/Features/Browsers/`)

`BrowserExtensionsViewModel` takes the new scanner and runs it on every refresh before the add-on scan;
if the settings check throws, the add-on list still shows. New `HijackRowViewModel`; `BrowserSectionViewModel`
carries the rows. Each browser card shows "Start-up and search settings" first: one row per setting with
status icon + text (never colour alone), the address host and profile, and, for Changed/Forced rows, the
advice and an "Open <browser>'s settings" button (`AutomationProperties.Name` set). A one-line
summary under the add-on summary says either that everything looks normal or how many settings look changed
(and whether a setting on this PC forces some of them). Wording: "Changed" advice is "If you didn't
choose this, reset it in the browser's settings."; "Forced" advice says this is unusual on a home
computer and to ask the family member who looks after the PC.

## Tests

- `BrowserHijackClassifierTests`: every known provider, own pages, empty, unfamiliar hosts, look-alike
  hosts, bare hosts, `file:`/odd schemes, policy-forced, `ClassifyAll`.
- `MozLz4DecoderTests`: literal runs, long runs, match expansion (overlapping), and every bad-input path.
- `BrowserHijackScannerTests` (fixture files in a temp folder, fake policy reader): Chromium defaults and
  changed values, new-tab-as-home, restore-mode gating, Secure Preferences precedence, file held open,
  malformed file, policy override, known policy; Firefox prefs/user.js, search engine from a mozlz4 fixture,
  app-provided and legacy `current`, corrupt search file, `ParsePrefs`.
- `BrowserAddOnsOpenerTests`: settings URLs per browser and setting; browser not found.
- `BrowserExtensionsViewModelTests`: rows per browser, summary text, settings failure does not hide
  add-ons, open-settings success and fallback message.
- `AppCompositionTests` (real container, `ValidateOnBuild`) covers the new registrations, which are outside
  the DEBUG demo branch.

## Acceptance criteria

1. Chrome/Edge/Brave profile settings and Firefox profile settings are read and classified; the home page,
   start-up pages, new-tab page (where applicable) and search engine each show Looks fine / Changed / Forced.
2. Known providers and the browser's own pages are fine; any other host is Changed and shown by plain host
   name; look-alike hosts are not fine.
3. Registry policies for Chrome, Edge and Brave are read; an unfamiliar forced value shows the stronger
   "Forced by a setting on this PC" warning.
4. Firefox's default search engine is decoded from `search.json.mozlz4`; corrupt files are skipped and logged.
5. Files held open by a running browser are read; nothing is ever written to browser files or policies.
6. Each non-fine row has advice and a button that opens the browser's settings page; a missing browser
   gives a friendly fallback message.
7. Results never use colour alone; buttons have automation names; the page works at 900x600.
8. `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes.
