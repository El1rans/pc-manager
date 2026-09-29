# 20 - Read-only web console (branch `claude/local-web-console-stats-gnf9p9`)

Goal: let the PC's owner watch this PC's live stats from a browser on another device (phone,
laptop) - on the same network, or from anywhere through a VPN - **without being able to change
anything** on the PC from there.

## Design

- **Off by default.** Nothing listens on the network until the user turns the console on from the
  new "Web console" page. The choice is persisted (`WebConsoleSettings.Enabled`) and resumed at
  startup by `WebConsoleHostedService`.
- **Read-only by construction** (`Porchlight.Core/WebConsole/WebConsoleRouter.cs`): only `GET` and
  `HEAD` are accepted (any other method is `405` on every path), and the only routes are the page's
  three static files (`/`, `/app.js`, `/app.css`, embedded in `Porchlight.Core`) and
  `GET /api/stats`. No route calls anything that changes state - there is no fan control,
  lighting, update, install, kill-process or settings endpoint, and the stats collector only reads.
- **Access key.** `/api/stats` requires `Authorization: Bearer <key>`, compared in constant time.
  The 128-bit random key (`AccessKeyGenerator`) is created the first time the console is turned on,
  shown on the page, and can be replaced ("Make a new key") to lock out every device that had the
  old link. The page link carries the key in the URL `#fragment`, which browsers never send to the
  server, so it does not appear in request lines, logs or `Referer` headers. The page's static
  files are public (they contain no data).
- **No new dependencies, no admin rights.** A minimal HTTP/1.1 server on `TcpListener`
  (`WebConsoleServer`) instead of `HttpListener` (needs a URL ACL or admin to listen beyond
  localhost) or ASP.NET Core (would add the ASP.NET shared framework to the self-contained build).
  One request per connection; hard limits in `WebConsoleOptions`: 8 KB request head, 2 KB target,
  10 s read timeout, 16 concurrent connections. Strict request parsing (`HttpRequestHeadParser`)
  rejects absolute/over-long/non-ASCII targets, malformed or duplicated headers, and non-HTTP/1.x.
- **Security headers** on every response: `Cache-Control: no-store`, `X-Content-Type-Options:
  nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and a
  `Content-Security-Policy` that only allows the page's own script, style and `connect-src`.
  The page inserts every server value with `textContent`, never as HTML.
- **Listens on every interface** (IPv6 dual-mode where available) on a user-chosen port
  (default 8765, allowed 1024-65535). If the port is taken, the page says so and the user picks
  another; it never throws.
- **Stats** (`WebConsoleStatsCollector`) are sampled on demand, only while a browser is polling
  (every 2 s, paused while the tab is hidden), and cached for 1 s so several viewers share one
  sample. The collector owns its own `PerformanceSampler` and `ProcessMonitor` (both keep
  "previous sample" state for rates), separate from the dashboard's, so the two never skew each
  other and the console keeps working while the dashboard is paused (window minimized). After an
  idle gap (>10 s) it re-primes rate sources and waits 1 s so the first numbers are real
  per-second rates, not an average over the gap. Drives are re-read every 15 s and the pending
  restart check every 60 s; hardware tiles reuse the running `IHardwareService`'s latest snapshot
  via `HardwareSummarySelector`, so they match the Hardware page.

## What the page shows

Computer name, OS and uptime; CPU, memory, GPU, disk, download and upload tiles with 2-minute
sparklines; the Hardware page's "At a glance" tiles (temperatures, CPU power, hottest fan) with a
text severity label; the busiest apps; drives with free space (and an "Almost full" warning);
the pending-restart notice; and the system info table. Layout works from phone width up, and
follows the viewing device's light/dark setting.

## Out of scope / guidance for users

- No HTTPS: on a home network, anyone who can sniff the LAN could read the key. The page and the
  README advise using it on a trusted network, and a VPN such as Tailscale (not router port
  forwarding) to check in from elsewhere.
- Windows Firewall may ask the first time Porchlight listens; the page tells the user to allow it
  on private networks.

## Acceptance criteria

- [x] Off by default; turning it on starts the server and shows a copyable link with the key.
- [x] Browser with the link sees live stats; without (or with a wrong) key, sees nothing but a key
      prompt.
- [x] No request of any method to any path can change anything on the PC (`405` for non-GET/HEAD,
      `404` for unknown paths).
- [x] New key locks out old links; changing the port restarts the console there.
- [x] Port in use is reported on the page, not a crash.
- [x] Unit tests for parsing, routing, auth, controller, collector caching and an end-to-end
      loopback server test.
