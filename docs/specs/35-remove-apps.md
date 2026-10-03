# 35 - Remove apps (branch `feat/remove-apps`)

Windows' own "Installed apps" list is long, mixes in runtimes and drivers, and offers no hint about
what is safe to remove. This milestone adds a "Remove apps" page: a plain list of installed
programs with a search box, sorting, and a careful Remove button.

## Goals

- New page "Remove apps" (category `Apps & services`, `Order` 4, after Get apps / Running apps /
  Services; glyph `` Delete).
- A list of installed programs: name, publisher, size (if the program reported one), installed date
  (if known). Search filter; sort by name, size (largest first) or date (newest first).
- Remove: confirm first ("Remove <name>? This can't be undone from Porchlight."), then
  1. if `winget list` knows the app (see "winget id mapping"), run
     `winget uninstall --id <id> --exact --silent --disable-interactivity` through the existing
     `IWingetClient.UninstallAsync`; the result is shown with the same plain-language outcomes as the
     Updates page (`WingetExitCodes.DescribeOutcome`);
  2. otherwise (or when winget is missing) start the app's own uninstaller through the existing
     `IAppUninstaller` (the "Free up space" page): the program's own window runs, nothing is forced
     silent, and the list is refreshed when the user presses Refresh.
- The list refreshes after a winget removal.
- "System parts - usually keep": runtimes and drivers the PC needs are in a collapsed group
  (Remove is available but they are not in the main list). Porchlight's managed components
  (AnyDesk, OpenRGB, PawnIO) show "Managed by Porchlight" with no Remove button; Porchlight itself
  is never listed.
- "Often preinstalled" tag on apps that commonly come with a new PC (trial antivirus, game
  bundles, OEM helpers). It is a hint only - the wording never says the app is bad.
- DEBUG demo mode with a fake list and a fake remover (nothing is ever removed).

## Non-goals

- Usage tracking ("last used"). The only sources (Prefetch, UserAssist, file access times) are
  either unreliable (access times are off by default) or too invasive for a family PC; skipped.
- Microsoft Store (MSIX) apps, leftover file/registry cleanup, silent forcing of non-winget
  uninstallers, using `QuietUninstallString` (the program's own window is what the user expects).
- The "Recent changes" journal (another milestone): this one only exposes a seam (below).

## Design

### Reuse

`IInstalledAppsReader` / `InstalledAppFilter` (Free up space page) already read the three uninstall hives and
drop `SystemComponent=1`, entries without `UninstallString`, `ParentKeyName` and update/hotfix
release types, so Windows updates and hidden components never reach this page. The filter hides
Porchlight and its components entirely (right for "Large apps"); this page needs to see the
components to label them, so the reader gets `GetAllInstalledApps()` (same filtering, protected
names kept). `UninstallCommandParser` (CommandLineToArgvW rules, `MsiExec.exe /X{GUID}`, unquoted
paths with spaces) and `AppUninstaller` (never a shell string; per-user entries are never launched
while Porchlight is elevated) are reused unchanged.

### Core (`Porchlight.Core.RemoveApps`)

- `AppProtectionRules` (pure): `Classify(InstalledApp)` returns `RemovableAppKind`:
  `Porchlight` (hidden), `ManagedByPorchlight` (names from `ComponentCatalog`), `SystemPart`
  (Visual C++ Redistributable, .NET / ASP.NET Core / Windows Desktop runtimes, Windows App SDK,
  WebView2, Microsoft Edge, Windows-prefixed Microsoft items, and driver-like names/publishers:
  "Driver", "Chipset", "Management Engine", "PhysX", "AMD Software", Realtek), else `Normal`.
- `OftenPreinstalledCatalog` (pure data table): McAfee, Norton, WildTangent, Candy Crush,
  HP JumpStart, HP Support Assistant, Dell SupportAssist, Dell Digital Delivery.
- winget id mapping: `IWingetClient.ListInstalledAsync` runs
  `winget list --source winget --accept-source-agreements --disable-interactivity` and parses
  (Name, Id, Version) with the shared `WingetTableParser.ParseRows`. `WingetIdMapper` (pure)
  matches a registry app to a row by normalised name (a winget name cut off with an ellipsis matches
  as a prefix); if several rows share the name the version must match too, otherwise no id is
  returned (no guessing). Only `winget`-source rows are listed, so ids are always safe for
  `--exact`.
- `IRemoveAppsService` / `RemoveAppsService`:
  `ListAsync(ct)` (registry read off the UI thread + winget map; a winget failure just means no
  ids), `RemoveAsync(app, ct)` returning `RemoveAppOutcome` (`Removed`, `UninstallerOpened`,
  `Refused`, `BlockedWhileElevated`, `InvalidCommand`, `Failed`, `WingetProblem` + the winget
  outcome). It refuses anything that is not `Normal` or `SystemPart`, and anything not in the last
  `ListAsync` result. The winget call never gets a cancellable token once started (same rule as the
  Updates page).
- Seam for the "Recent changes" journal: `event EventHandler<AppRemovedEventArgs>? AppRemoved`,
  raised once after a confirmed winget removal. The journal subscribes there; nothing here depends
  on it.
- `AddRemoveAppsCore()` DI extension; the demo fakes live inside `#if DEBUG` only, real
  registrations are outside it.

### Admin

HKLM (per-machine) apps usually need administrator rights. Porchlight does not elevate itself for
this: winget and the apps' own uninstallers raise their own UAC prompt, and a declined prompt
comes back as a plain message ("Windows asked for permission and it wasn't given."). Per-user apps
are not removed while Porchlight itself runs as administrator (a user-writable command must never
run elevated); the row then says to restart Porchlight normally.

### App (`Porchlight.App/Features/RemoveApps`)

`RemoveAppsViewModel` (`Title` "Remove apps"): `Apps` (filtered, sorted, normal apps),
`SystemParts` (collapsed `Expander`), `Filter`, `SortOptions`, summary, status line. Implements
`IBusyGuard` while a removal runs. `AppRowViewModel`: size/date/hint texts, `CanRemove`,
automation names. View works at 900x600; every control has an `AutomationProperties.Name`.

## Tests

`AppProtectionRulesTests`, `OftenPreinstalledCatalogTests`, `WingetIdMapperTests`,
`WingetInstalledListParserTests`, `InstalledAppFilterTests` additions (protected names kept on
request), `RemoveAppsServiceTests` (winget path, fallback path, refused kinds, elevation,
unmapped, winget missing, event raised only on success), `RemoveAppsViewModelTests`, plus the real
container in `AppCompositionTests`. Nothing ever starts a real process or removes anything.

## Acceptance criteria

- [ ] The list shows real programs only (no updates, hidden components, entries without an
      uninstall command); search and the three sorts work.
- [ ] Remove asks first; uses winget when the id is known, otherwise opens the app's own uninstaller.
- [ ] Porchlight is never listed; AnyDesk/OpenRGB/PawnIO say "Managed by Porchlight" and cannot be
      removed here; runtimes and drivers sit in a collapsed group.
- [ ] "Often preinstalled" appears only as a hint.
- [ ] Declined UAC, missing winget or a failed uninstall never crash and give a plain message.
- [ ] DEBUG demo mode works; the real container builds (`AppCompositionTests`).
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
