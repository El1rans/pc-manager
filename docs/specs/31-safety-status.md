# 31 - "Is this PC safe?" (branch `feat/safety-status`)

A family member (and their helper) wants one calm answer to "is this PC safe?". This milestone adds a
"Safety" page that checks three things in plain words: the security software, Windows Update, and
which remote-control tools could let someone connect to the PC.

## Goals

- New page "Safety" (category `Internet & safety`, `Order` 0, glyph Shield).
- Three cards, each backed by its own Core service behind an interface (`Porchlight.Core.Safety`):
  1. **Security status** - antivirus on and up to date, firewall on, read from Windows Security Center.
     One verdict line ("This PC is protected" / "Antivirus is off" / "Antivirus is out of date" ...)
     plus one detail line per product. Button "Open Windows Security" (`windowsdefender:`).
  2. **Windows Update** - when Windows last installed updates, whether a restart is waiting, whether
     updates keep failing. Button "Open Windows Update" (`ms-settings:windowsupdate`). Pending updates
     are counted only on an explicit "Check now" (a search can take minutes), with a timeout.
  3. **Who can connect to this PC?** - remote-access tools installed or running (TeamViewer, AnyDesk,
     RustDesk, UltraViewer, Splashtop, Chrome Remote Desktop, LogMeIn, ConnectWise/ScreenConnect,
     Supremo, Quick Assist). The AnyDesk that Porchlight set up is labelled "Set up by Porchlight".
     Anything else gets a calm warning: "If you didn't set this up, someone may be able to control this
     PC. Ask your family helper." Read-only: no uninstall here.
- `ISafetyStatusService` returns an aggregate (`SafetyStatus`) so a later web console change can reuse it.
- Refresh on every navigation, plus a "Refresh" button. DEBUG demo mode with fakes.

## Non-goals

- Changing any security setting, starting scans or updates, uninstalling anything.
- Changing the Updates page (it covers apps via winget only).
- Detecting Remote Desktop (RDP) or malware.

## Design

### Security (`ISecurityStatusService`)

- `ISecurityCenterReader` reads WMI `root\SecurityCenter2` `AntiVirusProduct` / `FirewallProduct`
  (`displayName`, `productState`) off the UI thread. A missing namespace (Windows Server, no Security
  Center) or any expected WMI failure gives "unavailable", not an exception. Security Center usually
  lists only third-party firewalls, so it is not enough for Windows' own.
- `IWindowsFirewallReader` reads the built-in Windows Firewall directly (COM `HNetCfg.FwPolicy2`:
  `FirewallEnabled[profile]` for Domain/Private/Public and `CurrentProfileTypes`), off the UI thread,
  read-only, with a timeout. A failure is logged and gives null.
- `ProductStateParser` (pure) decodes the `productState` bitfield: bits 12-15 = state (0 off, 1 on,
  2 snoozed, 3 expired), bits 4-7 = definitions (0 up to date, otherwise out of date).
- `SecurityVerdictBuilder` (pure) turns the products into the verdict line and a level. Priority:
  antivirus off > antivirus out of date > firewall off > protected. The firewall is fine when a
  third-party firewall product is on or Windows Firewall is on for every active profile; otherwise the
  verdict is "Windows Firewall is off". With no readable firewall source at all the verdict is
  "Couldn't check". A "Windows Firewall - on/off" detail line is shown. Several antivirus products are fine:
  one that is on and current is enough (Windows turns Defender off when another one is installed).

### Windows Update (`IWindowsUpdateStatusService`)

- `IWindowsUpdateAgent` is the only class touching the Windows Update Agent COM API
  (`Microsoft.Update.Session` -> `QueryHistory`) and the `RebootRequired` registry key. Pending count
  is `Search("IsInstalled=0 and IsHidden=0")`, only from `CheckPendingAsync`, abandoned after
  `SafetyTimeouts.PendingUpdateSearch`.
- `UpdateHistorySummarizer` (pure): last successful install (Defender definition updates ignored),
  failed attempts in the last 30 days. Concern when 3 or more attempts failed recently, or nothing was
  installed for 60 days. Named constants.

### Remote access (`IRemoteAccessService`)

- `RemoteToolCatalog` is a data table: process names, uninstall `DisplayName` fragments and service
  name fragments per tool. `RemoteToolMatcher` (pure) matches collected evidence against it.
- `IRemoteToolProbe` gathers the evidence, reusing `IProcessSnapshotSource`, `IInstalledAppsReader`
  and the service list. `RemoteAccessService` marks AnyDesk "Set up by Porchlight" when
  `IComponentService` reports it present.

### App (`Porchlight.App/Features/Safety`)

`SafetyViewModel` loads the three cards independently (a slow one never blocks the others), shows a
headline from `SafetyStatusSummary`, status as icon + text, `AutomationProperties.Name` on controls,
works at 900x600.

## Tests

`ProductStateParserTests`, `SecurityVerdictBuilderTests`, `UpdateHistorySummarizerTests`,
`RemoteToolMatcherTests`, service tests with fakes (`RemoteAccessService`, `SafetyStatusService`
aggregation), and the existing `AppCompositionTests` (real container, `ValidateOnBuild`).

## Acceptance criteria

- [ ] Page appears under Internet & safety; all three cards render with plain text at 900x600.
- [ ] Verdict reflects antivirus off / out of date / Windows Firewall off / protected;
      "Couldn't check" when neither Security Center nor the firewall can be read.
- [ ] Update card shows last install date and failure warning; "Check now" counts pending updates and
      times out gracefully; nothing slow runs on page load.
- [ ] Remote tools are listed with a calm warning; Porchlight's AnyDesk says "Set up by Porchlight".
- [ ] Build has zero warnings, tests pass, real services construct outside demo mode.
