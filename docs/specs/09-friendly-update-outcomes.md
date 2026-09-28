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
