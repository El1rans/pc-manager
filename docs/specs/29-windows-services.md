# 29 - Windows services (branch `feat/windows-services`)

Many apps install background services (updaters, sync agents, game launchers, printer helpers)
that run all the time. This milestone adds a "Services" page that explains them in plain words and
lets the user stop or turn off the ones that came with third-party apps - without letting them
break Windows.

## Goals

- New page "Services" (category `Apps & services`, `Order` 3, glyph `` Admin or similar gear).
- By default the list shows only **services not from Microsoft** (the ones a user may actually want
  to change). A "Show Windows services" toggle adds the Microsoft ones (read-only).
- Each row: display name, plain description (the service's own description, first sentence,
  else "No description"), publisher (from the service executable's `CompanyName`), status as icon +
  text ("Running" / "Stopped" / "Starting..." / "Stopping..."), and start type in plain words
  ("Starts with Windows", "Starts with Windows (delayed)", "Starts when needed", "Turned off").
- Actions for non-Microsoft services: Start, Stop, Restart, and change start type to
  "Starts with Windows" / "Starts when needed" / "Turned off". Stopping or turning off shows a
  short warning ("The app that installed this may stop working properly until you turn it back on.").
- Everything that changes a service needs administrator rights: when not elevated, the action
  buttons are disabled and the shared `AdminRequiredBanner` is shown (only when at least one
  changeable service is listed).
- Search filter and a "Refresh" button. Refresh on every navigation to the page.
- DEBUG demo mode with a fake list.

## Non-goals

- Changing Microsoft/Windows services in any way, deleting services, editing the logon account,
  recovery options or dependencies, driver services (`SERVICE_KERNEL_DRIVER`/`FILE_SYSTEM_DRIVER`).

## Design

### Safety rules

- A service is "Microsoft" when its executable's `CompanyName` starts with "Microsoft", or its
  executable is inside the Windows folder (this covers `svchost.exe`-hosted services). Microsoft
  services are never changed - the service layer refuses (`Refused`), independent of the UI.
- Also always refused: Porchlight's own components' services if any (check `IComponentService`
  for AnyDesk/OpenRGB/PawnIO service names - their pages manage them), and any service name not in
  the last `ListAsync` result.
- Not elevated → `NeedsAdmin` without attempting anything.
- Start type changes go through `ChangeServiceConfig` (P/Invoke, `SERVICE_NO_CHANGE` for everything
  but the start type; delayed auto-start via `ChangeServiceConfig2` with
  `SERVICE_CONFIG_DELAYED_AUTO_START_INFO`) - not `sc.exe` string building. Start/stop through
  `System.ServiceProcess.ServiceController` with a timeout (named constant, e.g. 30 s); a stop of a
  service with running dependents is refused with a plain message naming them.

### Core (`Porchlight.Core.WindowsServices`)

- Package: `System.ServiceProcess.ServiceController` (exact version in `Directory.Packages.props`
  matching the .NET 10 band). WMI is already used elsewhere (check `System.Management` in Core) -
  use `Win32_Service` (`Name`, `DisplayName`, `Description`, `PathName`, `StartMode`,
  `DelayedAutoStart`, `State`, `ServiceType`) for listing because it returns resolved descriptions.
- `ServiceImagePathParser` (pure): extracts the executable from `PathName` (quoted path,
  unquoted path with spaces up to `.exe`, env vars, `\??\` prefix). Reuse/extend
  `Porchlight.Core.Startup.StartupCommandParser` if it already covers these rather than duplicating.
- `ServiceClassifier` (pure): Microsoft vs third-party per the safety rules; plain start-type and
  status labels; first-sentence description shortening.
- `IServiceManager` / `ServiceManager` - the only class that touches the SCM (start, stop, set start
  type). `IServiceInfoSource` / `WmiServiceInfoSource` - listing. Both behind interfaces for tests.
- `IWindowsServicesService` / `WindowsServicesService`: `ListAsync(ct)` (off the UI thread) and
  `StartAsync/StopAsync/RestartAsync/SetStartTypeAsync(name, ...)` returning
  `ServiceChangeResult` (`Changed`, `NotFound`, `Refused`, `NeedsAdmin`, `HasDependents`,
  `TimedOut`, `Failed`).
- `AddWindowsServicesCore()` DI extension (fake under DEBUG demo mode).

### App (`Porchlight.App/Features/WindowsServices`)

- `WindowsServicesViewModel` (`Title` "Services"): `Services` (filtered view), `ShowWindowsServices`,
  `Filter`, `IsElevated` via `IShellService`, summary ("14 services from other apps - 9 running"),
  `AdminRequiredBanner` visibility, busy state per row while an action runs, friendly result line.
- `ServiceRowViewModel`: labels, `CanChange`, commands; start type as a `ComboBox` with the three
  plain options (applied on selection, reverted on failure).
- View: scrolling list, works at 900x600, `AutomationProperties.Name` on every control naming the
  service.

## Tests

`ServiceImagePathParserTests`; `ServiceClassifierTests` (Microsoft by company, by Windows folder,
svchost; labels for every start mode incl. delayed); `WindowsServicesServiceTests` with fakes
(refuses Microsoft, refuses unknown, NeedsAdmin when not elevated, dependents refused, timeout,
start type mapping incl. delayed); `WindowsServicesViewModelTests` (default hides Microsoft, toggle,
filter, banner only when relevant, combo revert on failure).

## Acceptance criteria

- [ ] The page lists non-Microsoft services by default with description, publisher, status and start
      type in plain words; the toggle shows Microsoft services read-only.
- [ ] Start/Stop/Restart/start-type changes work for third-party services when elevated, with a
      warning before stopping or turning off.
- [ ] Microsoft services, unknown names and driver services are never changed (service-level refusal).
- [ ] Not elevated: actions disabled and the admin banner explains why.
- [ ] DEBUG demo mode works.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
