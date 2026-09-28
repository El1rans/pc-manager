# 09 - Friendly update outcomes and Reinstall (branch `feat/friendly-update-outcomes`)

Found during real-world testing: winget refuses some upgrades, and Porchlight shows raw codes like "Failed (0x8A15008E)" that mean nothing to a non-technical parent.

Observed on the maintainer's PC:
- `JanDeDobbeleer.OhMyPosh` -> exit `0x8A15008E`: "A newer version was found, but the install technology is different from the current version installed. Please uninstall the package and install the newer version."
- `RARLab.WinRAR` -> exit `0x8A15002B`: "No applicable upgrade found. A newer package version is available in a configured source, but it does not apply to your system or requirements."

## Goals

1. Every winget outcome on the Updates page is a short plain-language sentence, never a bare hex code. The code stays in a tooltip and in the log.
2. For the "install technology mismatch" case, offer a safe, confirmed **Reinstall** (uninstall, then install the latest version).
3. For "no applicable update", explain it and offer **Hide this update** (uses the existing ignore list).

## Core (`Porchlight.Core/Winget`)

- `WingetOutcome`: `Kind` (Updated, UpdatedRestartNeeded, NoApplicableUpdate, ReinstallRequired, AppRunning, Cancelled, NeedsAdmin, Blocked, NetworkProblem, Failed), `Title` (short, e.g. "Needs a reinstall"), `Explanation` (1-2 plain sentences), `ExitCode`, `SuggestedAction` (None, Reinstall, Hide, Retry, CloseAppAndRetry, RestartPc).
- Mapping from winget's official return codes (https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md). Verify every constant against that document - do not guess. Cover at least: success, restart required, no applicable upgrade, install technology mismatch (0x8A15008E per the observed log - verify its symbolic name), app/package in use, cancelled / UAC declined, installer failed, blocked by policy, download/network failures, hash mismatch, no applicable installer, elevation-related codes, pinned packages. Unknown -> `Failed`: "Something went wrong while updating. Details are in the log." with the code in the tooltip.
- Output text is a secondary signal only when a code is ambiguous; the code wins.
- Extend the existing shared `Porchlight.Core.Processes.WingetExitCodes` - no second table.
- `IWingetClient.UninstallAsync(id, log, ct)` and `InstallAsync(id, silent, log, progress, ct)`: `winget uninstall --id <id> --exact --disable-interactivity [--silent]`; `winget install --id <id> --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity [--silent]`. Same rule as upgrades: once winget has started, never kill it (CancellationToken.None).
- `ReinstallWorkflow` (pure orchestration, unit-tested with fakes): uninstall -> if it fails, stop and report (app still installed, nothing lost) -> install -> if install fails after a successful uninstall, report the critical state "The old version was removed but the new one didn't install" with `SuggestedAction = Retry` (install only). Never loop automatically.

## App (`Porchlight.App/Features/Updates`)

- Status column: icon + short `Title` ("Needs a reinstall", "Not available for this PC", "Close the app and try again", "Updated - restart needed"). Tooltip = `Explanation` + "(winget code 0x...)". Also `AutomationProperties.HelpText`.
- Row actions for `SuggestedAction` (inline in the row, or a details panel for the selected row below the grid - whichever stays readable at 900x600):
  - **Reinstall...** -> confirmation dialog: "Reinstall <App>? Porchlight will uninstall <App> and then install the newest version. Your settings for <App> are usually kept, but this can't be guaranteed. Close <App> before continuing." Buttons **Reinstall** / **Cancel**. One at a time; disabled during an update run; progress + log like updates; quiet re-check after success.
  - Install failed after uninstall -> row shows critical "Not installed - the new version didn't install" + **Try install again**.
  - **Hide this update** -> adds the id to the ignore list and shows "WinRAR won't be shown again. You can bring it back with 'Show ignored'."
  - **Try again** for Retry / CloseAppAndRetry.
- Summary in plain words: "Finished: 1 updated, 1 needs a reinstall, 1 not available for this PC".
- Everything still logged with codes.

## Out of scope

Automatic or bulk reinstall; changing install scope/architecture.

## Tests

Mapping (every mapped code -> Kind/SuggestedAction; unknown -> Failed); ReinstallWorkflow (uninstall fail -> stop; install fail after uninstall -> critical + retry; success; cancellation only before start); ViewModel (Reinstall disabled during a run, confirmation required, Hide uses ISettingsStore.Update, summary wording).

## Acceptance criteria

- [ ] No bare hex code is ever the only thing in the Status column.
- [ ] Mismatch shows "Needs a reinstall" + Reinstall...; no-applicable shows "Not available for this PC" + Hide this update.
- [ ] Reinstall requires confirmation; a failure after uninstall is clearly reported with Try install again.
- [ ] Agents never uninstall/install/upgrade real packages; the maintainer tests Reinstall on Oh My Posh.

## Addendum: four fixes from real testing (branch `feat/updates-testing-fixes`)

Found during the maintainer's manual testing of the above.

### 1. Remember last outcome across restarts

Row actions (Reinstall.../Hide/Try again) used to only appear after an update failed in the
*current* session - restarting the app lost them and the user had to fail the update again to get
them back.

Fixed by persisting the last failed outcome per package, keyed by package id + the `Available`
version it was attempted against, in `UpdatesSettings.LastOutcomes`
(`List<PersistedUpdateOutcome>`) via the existing `ISettingsStore` - a dedicated store was not
worth it: settings already provide atomic, best-effort JSON persistence and a single shared
in-memory instance, and this is a small, bounded list that belongs with the rest of the Updates
feature's settings. `Core.Winget.UpdateOutcomeMemory` holds the (settings-store-free, directly
unit-tested) `Find`/`Remember`/`Forget`/`Prune` logic. On every refresh, a row whose id + available
version matches a remembered entry shows that entry's status/actions again - not just when the
view model itself has no in-session state for it, so a `Remember` from earlier in the very same run
also takes effect on the very next (even quiet) refresh. An entry is cleared on a successful
update/reinstall (or superseded by a new one), and on every refresh the list is pruned down to only
the package ids winget currently lists, plus a hard cap (`UpdateOutcomeMemory.MaxEntries`) as a
backstop.

### 2. Reinstall for "not available for this PC"

Real case: `RARLab.WinRAR` (6.24 installed) - winget refuses the upgrade to 7.23 with
`APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE`. Reinstalling (uninstall, then install the newest
version - the existing `ReinstallWorkflow`) can succeed where a plain upgrade can't, so this case
now offers **Reinstall...** alongside **Hide this update**, and the explanation mentions that
reinstalling may help.

`WingetSuggestedAction` became a `[Flags]` enum (`None`, `Reinstall`, `Hide`, `Retry`,
`CloseAppAndRetry`, `RestartPc`) rather than adding a second field or a list - the cleanest fit for
"usually one action, sometimes two", and `UpdatePackageViewModel.CanReinstall`/`CanHide`/`CanRetry`
now test with `HasFlag` instead of equality. The row actions panel already showed
Reinstall/Hide/Retry as independent, individually-visible buttons, so no XAML changes were needed
for more than one to show at once. Only `APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE` gets both
actions; the other "no applicable update" codes (`NoApplicableInstaller`, `PackageIsPinned`) keep
Hide only, since a mismatch-of-technology reinstall isn't a fix for those.

### 3. Name the programs holding files for "app in use"

Real case: `APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE_BY_APPLICATION` (installer exit 6,
"Files modified by the installer are currently in use") for OBS Studio, while OBS itself was **not**
running - Chrome and another app had OBS's virtual-camera DLL
(`obs-plugins\win-dshow\obs-virtualcam-module64.dll`) loaded. "Close the app and try again" was
misleading since closing OBS (already closed) would not have helped.

`Core.Processes.IAppInUseDiagnosticsService` (`AppInUseDiagnosticsService`) now looks up the
package's install location from the uninstall registry (the existing `Components.IRegistryReader` -
already behind an interface, already the app's one source of ARP data, so no new registry-access
type was needed), enumerates a bounded set of `*.dll`/`*.exe` files under it, and asks the Windows
Restart Manager API (`IAppLockDetector`/`RestartManagerLockDetector`: `RmStartSession` /
`RmRegisterResources` / `RmGetList` via P/Invoke on `rstrtmgr.dll`) which processes have them open -
the same mechanism Explorer/MSI use for their own "these programs need to close" dialogs. Never
terminates anything it finds. Friendly names come from `RM_PROCESS_INFO.strAppName`, deduplicated
and capped to `AppInUseExplanation.MaxNamedProcesses` (5). The Updates page enriches the outcome
only for `AppInUseByAnotherApplication` specifically, and only after getting the base outcome (so
the pure `WingetExitCodes.DescribeOutcome` mapping needs no package-context parameters); a failed
or empty lookup falls back to the previous, generic "close the app" text
(`AppInUseExplanation.GenericExplanation`) - e.g. "These programs are using OBS Studio's files:
Chrome, Claude. Close them, then try again."

### 4. Explain a hidden UAC prompt

Real case: `Google.CloudSDK`'s update sat with no output for several minutes, apparently hung - it
was actually waiting on a UAC prompt that winget's background process had raised, which (with no
window of its own to bring to the foreground) only ever showed as a flashing taskbar icon.

Winget's own live output includes a line like "The installer will request to run as administrator.
Expect a prompt." before this happens - `WingetExitCodes.MentionsAdminPromptRequest(line)` matches
it robustly on the `"request to run as administrator"` substring rather than the whole sentence.
`UpdatesViewModel.CreateLogProgress` watches each line as it streams in and sets
`WaitingHint`/`AdminPromptWaitingHintText` ("Waiting for your permission - look for the Windows
prompt on the taskbar") the moment it's seen, clearing it again as soon as any further output line
arrives; `WaitWithHintAsync`'s existing two-minute-silence hint no longer overwrites this more
specific one if it's already showing. If Porchlight itself is already running elevated this line is
never printed (nothing left to elevate), so no special-casing for that was needed.

### 5. "Unknown" installed-version packages updating forever

Real case: `Google.CloudSDK` (only listed because of "Include apps with unknown version") showed
Unknown -> 586.0.0; the update reported success (exit 0), but the row reappeared on the next
refresh, and updating it again actually installed a **second**, per-user copy alongside the
already-current machine-wide one - Porchlight has no installed-version string to compare against
for this package, so it cannot tell a real upgrade from a redundant reinstall.

When a package whose `InstalledVersion` is `"Unknown"` reports success, `UpdatesViewModel`
remembers it (reusing fix 1's `UpdateOutcomeMemory`) as "already updated to `<version>`" instead of
forgetting it, and `UpdatePackageViewModel.IsPendingVersionConfirmation` hides the row by default -
discoverable via the same "Show ignored" toggle as an explicitly ignored row
(`IsHiddenByDefault = IsIgnored || IsPendingVersionConfirmation`) rather than a second toggle. Since
the remembered entry is keyed to that available version (fix 1), the row reappears normally, with
no special-casing needed, the moment winget reports a newer one. The Notes column for every
unknown-installed-version row (not just this hidden state) now also warns "Current version unknown
- updating may install a second copy".
