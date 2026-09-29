# 18 - Internet page (branch `feat/network`)

An "Internet" page for a non-technical user: is my connection working, why not, fix it for me,
how fast is it, and which programs are using it. Plain language throughout; nothing disruptive
runs without an explicit, explained confirmation. Follows `00-engineering-standards.md`.

## Goals

- **Status card:** connected yes/no, connection type (Wi-Fi / cable), and for Wi-Fi the network
  name and signal as four bars plus plain words ("Excellent", "Good", "Fair", "Weak"). Also the
  local IP, the router (gateway) and DNS servers.
- **"Fix my internet" troubleshooter:** a guided, one-button check that runs four steps in order -
  network adapter up, router reachable, name lookup (DNS) working, internet reachable - shows a
  plain result for each, then a diagnosis with only the remedies that match the failure.
- **Remedies:** flush DNS cache; renew the IP address (warns the connection drops for a few
  seconds); reset the network adapter (needs administrator, confirmation first). As a last resort
  the page only *offers* to open Windows' Network settings page; it never runs a network reset.
- **Speed test:** explicit button only, with a visible note that it contacts Cloudflare. Measures
  latency, download and upload in about 10 seconds, is cancellable, shows Mbps and a plain
  verdict ("Good for video calls"). The last result is remembered in settings.
- **Apps using the network:** per program, the number of active connections, refreshed every few
  seconds while the page is visible and stopped when it is not. Read-only.
- DEBUG demo mode (`PORCHLIGHT_DEMO_DATA=1`) fakes all of the above so screenshots never show the
  real network name, addresses or program list.

## Non-goals

- **Public IP address.** Not fetched by default (privacy); it is not shown at all.
- **Per-app bandwidth.** Needs ETW / a kernel session; only connection counts are shown.
- **Ending connections or killing programs** from the apps list. Read-only.
- **Running Windows' "Network reset"** (it removes all adapters and VPN clients). Only offered by
  opening `ms-settings:network-status`.
- Proxy / VPN / firewall diagnosis, Wi-Fi channel analysis, joining networks.
- Continuous background speed tests or any automatic network traffic beyond the troubleshooter
  the user starts.

## Design

### Core (`src/Porchlight.Core/Network/`)

Status
- `NetworkStatus` record (`IsConnected`, `ConnectionType`, `AdapterName`, `Wifi`, `LocalIp`,
  `Gateway`, `DnsServers`) and `INetworkStatusProvider.GetStatus()`.
  `NetworkStatusProvider` uses `System.Net.NetworkInformation`, picking the best interface:
  up, not loopback/tunnel, preferring one with an IPv4 gateway.
- `IWifiInfoReader` / `WifiInfoReader`: the Native Wifi API (`wlanapi.dll`:
  `WlanOpenHandle`, `WlanEnumInterfaces`, `WlanQueryInterface` with the current-connection
  opcode) rather than parsing `netsh wlan show interfaces`, which is localized. Returns
  `WifiInfo(Ssid?, SignalPercent)`. On Windows 11 24H2+ the SSID can be withheld without location
  permission: the SSID is then null and the UI says "Name hidden by Windows" - never an error.
- `WifiSignalDescriber` (pure): percent -> 0-4 bars + words.

Troubleshooter
- `INetworkProbe`: `IsAdapterUp()`, `PingGatewayAsync`, `ResolveDnsAsync`, `CheckInternetAsync`.
  The internet step requests `http://www.msftconnecttest.com/connecttest.txt` (what Windows'
  connectivity check uses; plain HTTP on purpose so captive portals can be detected) without
  following redirects, and expects the body `Microsoft Connect Test`. A redirect or different
  body is a `CaptivePortal` result (the Wi-Fi wants a sign-in), not "no internet".
- `NetworkDiagnosis.Diagnose(results)` (pure state machine) -> `Diagnosis(Outcome, Summary,
  Advice, Remedies)`:

  | Situation | Outcome | Remedies offered (in order) |
  |---|---|---|
  | internet step passed | `Healthy` | none |
  | internet reports captive portal | `SignInRequired` | none (advice: open a browser and sign in) |
  | adapter down | `AdapterDown` | reset adapter, Windows network settings |
  | adapter up, router unreachable | `RouterUnreachable` | renew IP, reset adapter |
  | router ok, DNS failed | `DnsProblem` | flush DNS, renew IP |
  | router + DNS ok, internet failed | `InternetUnreachable` | renew IP (advice: restart the router / call provider), Windows network settings |

  A blocked router ping while internet works is still `Healthy` (many routers ignore pings).
  Steps after a failed adapter check are `Skipped`.
- `INetworkTroubleshooter.RunAsync(IProgress<TroubleshootStepUpdate>, ct)` runs the probes
  in order and returns a `TroubleshootReport` (per-step results + diagnosis).
- `INetworkRemedyService`: `FlushDnsAsync`, `RenewIpAsync`, `ResetAdapterAsync(adapterName)`,
  returning `RemedyResult(Outcome, Message)` (`Done`, `NeedsAdmin`, `Failed`). All processes via
  `IProcessRunner` (`ipconfig /flushdns`; `ipconfig /release` then `/renew`; `netsh interface
  set interface name=<n> admin=disabled` then `admin=enabled`). Reset refuses when not elevated
  and rejects adapter names with quotes/control characters; the "enabled" call always runs
  (with `CancellationToken.None`) once the adapter has been disabled, so a failure can never
  leave the adapter off.

Speed test
- `ISpeedTestService.RunAsync(IProgress<SpeedTestProgress>, ct)` -> `SpeedTestResult(Latency,
  Download Mbps, Upload Mbps, MeasuredAt)` with null members for phases that failed; null
  overall result if everything failed. Latency: median of 5 tiny `__down?bytes=0` requests.
  Download: `https://speed.cloudflare.com/__down?bytes=N` with growing N inside a 5 s budget.
  Upload: POST to `https://speed.cloudflare.com/__up` with growing payloads inside a 4 s budget.
  Time comes from an injected `TimeProvider`, the transport from an injected
  `HttpMessageHandler`, so tests use fakes.
- `SpeedMath.ToMbps(bytes, elapsed)` (pure, megabits = 1,000,000 bits) and
  `SpeedVerdictDescriber.Describe(result)` (pure): level + plain sentence, mentions high delay.
- Persisted as `AppSettings.Network.LastSpeedTest` (`NetworkSettings`).

Apps using the network
- `TcpTableParser` (pure): parses the byte buffers returned by `GetExtendedTcpTable` (IPv4 and
  IPv6, owner-PID variants) into `NetworkConnection(Pid)`. Only established TCP connections whose
  remote end is not loopback are kept.
- `INetworkConnectionReader` / `NativeNetworkConnectionReader` (the P/Invoke, with the standard
  grow-and-retry on `ERROR_INSUFFICIENT_BUFFER`), `IProcessNameResolver`, and
  `NetworkAppUsageAggregator` (pure): group by friendly process name, sort by connection count.
  UDP (`GetExtendedUdpTable`) is deliberately not read: a bound UDP socket says nothing about
  actual traffic and would only list nearly every program.
- `INetworkAppUsageService.GetUsage()` ties the three together. Unreadable processes are
  skipped; failures are logged at Debug.

`AddNetworkCore()` registers everything; DEBUG builds swap in `Demo*` fakes when
`DemoDataMode.IsEnabled`.

### App (`src/Porchlight.App/Features/Network/`)

- `NetworkFeature.AddNetworkFeature()` -> `AddNetworkCore()` + `AddPage<NetworkViewModel,
  NetworkView>()`. Title "Internet", `Order = 7`, globe glyph.
- `NetworkViewModel` (`IBusyGuard`, `IDisposable`): status card, troubleshooter, remedies,
  speed test, app list. Status refreshes on navigation and via a Refresh button. The app list
  refreshes every 3 s only while the view is visible (`SetPageVisible`, driven by the view's
  `IsVisibleChanged`); the loop is cancelled otherwise. Busy while the troubleshooter, a remedy
  or a speed test runs (closing the app asks first).
- Remedies that interrupt the connection show an inline confirmation ("Your internet will drop
  for a few seconds. Continue?"); reset adapter also shows `AdminRequiredBanner` when not elevated
  and disables its button. Every step shows an icon plus a word (never colour alone).
- The layout works from 900x600 and scrolls.

## Acceptance criteria

1. The Internet page is Order 7 and shows connected/not connected, type, Wi-Fi name (or "Name
   hidden by Windows"), 4-bar signal with words, local IP, router and DNS servers. No public IP.
2. "Fix my internet" runs the four steps in order, each showing a result with icon + text, then a
   diagnosis with only the remedies from the table above.
3. Diagnosis for every combination of step results is covered by unit tests, including the
   blocked-ping and captive-portal cases.
4. Flush DNS runs `ipconfig /flushdns`; Renew IP warns first; Reset adapter requires
   administrator and confirmation, and always re-enables the adapter it disabled. Network reset
   is only offered (opens `ms-settings:network-status`).
5. Speed test only runs on button press, says it contacts Cloudflare, can be cancelled, finishes
   in about 10 s, shows Mbps and a plain verdict, and the last result is shown after restart.
6. Throughput math, verdict text, TCP table parsing, aggregation and the speed-test flow (fake
   handler, fake clock) have unit tests; no test touches the network or runs ipconfig/netsh.
7. The apps list groups connections by program, refreshes only while visible, and is read-only.
8. `dotnet build -c Release` has zero warnings; `dotnet test -c Release` passes; README updated.
