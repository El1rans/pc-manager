# 27 - Get apps (branch `feat/get-apps`)

Installing a common app today means a browser, a search engine, and avoiding fake download sites -
a real risk for non-technical users. Porchlight already drives winget, so this milestone adds a
"Get apps" page: type a name, pick the result, press Install.

## Goals

- New page "Get apps" (category `Apps & services`, `Order` 1, glyph `` Shop), first tab of
  the category.
- A search box. Search runs on Enter or the "Search" button (not on every keystroke - winget search
  takes seconds), needs at least 2 characters, and shows "Searching..." with a progress ring.
- Results list (max 50): name, version, the winget id in secondary text, and an "Install" button.
  Apps that are already installed show "Installed" (icon + text) instead of the button.
- When the box is empty, show a short curated "Popular apps" list (Google Chrome, Mozilla Firefox,
  VLC media player, 7-Zip, Zoom, Spotify, Adobe Acrobat Reader, Notepad++, WhatsApp, Discord) with
  the same Install/Installed state.
- One install runs at a time. While one is running its row shows progress text from winget and all
  other Install buttons are disabled. The page reports busy through `IBusyGuard` (same pattern as the
  Updates page) so the shell warns before closing mid-install.
- Result is a plain sentence on the row: "Installed" (success), or the friendly winget outcome
  from spec 09 (`WingetOutcome` / `WingetExitCodes`) with what to do next.
- Every finished install (success or failure) is added to the update history from spec 26 as an
  "Installed" entry, with the same explanation the row showed.
- DEBUG demo mode with fake search results and a fake install.

## Non-goals

- Microsoft Store (`msstore`) source - it needs Store agreements and account-bound licensing; only
  the `winget` community source is searched.
- Uninstalling apps, choosing install location/scope/version, ratings or screenshots.

## Design

### Safety rules

- Only ids returned by the last search (or the popular list) can be installed; the view model
  refuses anything else.
- Ids are passed through `ArgumentList` (`IProcessRunner`), always with `--exact --source winget`.
  The existing `IWingetClient.InstallAsync` already does this - reuse it.
- Never cancel a running install (same rule as `IWingetClient.InstallAsync` docs): navigating away
  does not stop it; the busy guard warns on exit.

### Core (`Porchlight.Core.Winget`)

- `IWingetClient.SearchAsync(string query, CancellationToken)` →
  `winget search --query <q> --source winget --count 50 --accept-source-agreements --disable-interactivity`.
  Returns `IReadOnlyList<WingetSearchResult>` (`Name`, `Id`, `Version`). Parse with the existing
  table parser if it fits (check `WingetTableParser`: it currently targets the upgrade table with
  Name/Id/Version/Available/Source columns); otherwise add `WingetSearchTableParser` sharing the
  column-splitting logic rather than copying it. "No package found matching input criteria." =
  empty list, not an error.
- `IWingetClient.ListInstalledIdsAsync(CancellationToken)` → `winget list --source winget
  --accept-source-agreements --disable-interactivity`, returning the set of installed ids
  (case-insensitive). Called once per page visit in the background; until it finishes the
  Installed state is simply not shown. After a successful install the id is added to the set.
- `PopularApps` - static list of (name, id) pairs; ids must be exact winget ids (e.g.
  `Google.Chrome`, `Mozilla.Firefox`, `VideoLAN.VLC`, `7zip.7zip`, `Zoom.Zoom`, `Spotify.Spotify`,
  `Adobe.Acrobat.Reader.64-bit`, `Notepad++.Notepad++`, `Discord.Discord`; WhatsApp only if a
  non-Store winget id exists - never use Store ids like `9N...`). Verify each id with `winget show --id <id> --exact --source winget` locally
  and keep only ones that resolve.
- Update `FakeWingetClient` (Demo) to implement the new members.

### App (`Porchlight.App/Features/GetApps`)

- `GetAppsFeature.AddGetAppsFeature()` - registers the page.
- `GetAppsViewModel` (`Title` "Get apps", category `Apps`, `Order` 1): `Query`, `SearchCommand`
  (cancels a previous search), `Results` / `PopularApps` (both `AppResultViewModel`),
  `IsSearching`, `StatusText` ("12 apps found", "No apps found for "xyz". Check the spelling or try
  a shorter name."), error text when winget is missing (reuse the Updates page's
  `WingetNotFoundException` handling/message).
- `AppResultViewModel`: `Name`, `Id`, `Version`, `IsInstalled`, `IsInstalling`, `ProgressText`,
  `ResultText`, `InstallCommand` (CanExecute false while any install runs).
- Silent install follows the Updates page's existing "silent" setting if there is one in
  `UpdatesSettings`; otherwise install non-silently (installers may show their own window).
- View: search box with placeholder "Search for an app, e.g. VLC", results in a scrolling list,
  works at 900x600, `AutomationProperties.Name` on every Install button ("Install VLC media player").

## Tests

Search parser tests on captured winget output (normal table, truncated names with `…`, "No package
found", CJK/wide names if the existing parser handles them); `WingetClient` argument tests via the
shared fake process runner (search args, list args); `GetAppsViewModelTests` (min length, search
results mapping, installed marking, only one install at a time, install refused for unknown id,
success marks installed, failure shows friendly text, busy guard while installing, winget missing).

## Acceptance criteria

- [ ] Searching shows winget-source results with name, version and Install / Installed.
- [ ] Empty search shows the curated popular apps (all ids verified to exist).
- [ ] Install runs one at a time through `IWingetClient.InstallAsync`, shows progress, never gets
      cancelled mid-install, and reports success or a friendly failure.
- [ ] Only ids from the last search / popular list can be installed.
- [ ] DEBUG demo mode works without running winget.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
