# 39 - Web console: more pages (branch `feat/web-console-more`)

The read-only web console (spec 21) shows live stats. A family helper looking at the PC from a
browser also wants to know three things that already exist in Porchlight: are app updates waiting,
what slows start-up, and does the PC look safe. This milestone adds those three read-only views.

## Goals

- Three new sections on the console page, each with its own read-only endpoint:
  1. **Updates waiting** (`GET /api/updates`) - the app updates found at the last check.
  2. **Startup impact** (`GET /api/startup`) - startup apps with on/off and impact (Low / Medium /
     High / Not measured), slowest first.
  3. **Security** (`GET /api/security`) - the Safety page's headline, antivirus/firewall,
     Windows Update status and remote-control programs found.
- A small nav bar at the top of the page (Overview, Security, Updates, Startup) that scrolls to
  each section.
- Reuse existing services; no duplicated logic. Strictly read-only.

## Non-goals

- Any action: no update, turn off, remove or fix buttons, no new POST (or any non-GET) endpoint.
- Changing the access key, pairing or auth model, or the security headers.
- Running a new `winget` scan from the console.

## Design

### Endpoints and auth

`WebConsoleRouter` gains `/api/updates`, `/api/startup` and `/api/security`. They follow exactly
the rule of `/api/stats`: `GET`/`HEAD` only (`405` otherwise), bearer access key required (`401`
without or with a wrong key, constant-time comparison), `no-store` and the other security headers.
The router now takes a second source, `IWebConsoleDetailsSource`.

### Data (`Porchlight.Core.WebConsole.WebConsoleDetailsCollector`)

- **Updates**: `IPendingUpdatesTracker` already remembers the count from the last check on the
  Updates page. It now also remembers the list (`PendingUpdate`: name, installed and available
  version; `Report(IReadOnlyList<PendingUpdate>, checkedAt)`, called by the Updates page). The
  console only reads it, so it never starts `winget` (a multi-second scan) and a request is an
  in-memory read. Before any check since Porchlight started, `hasChecked` is false and the page
  says to open Updates in Porchlight on the PC. The page shows when the list was last checked.
- **Startup**: `IStartupService.ListAsync` and `ImpactNeedsAdmin` (spec 13, impact from spec 30),
  sorted by impact (high first), then enabled before disabled, then name. Program paths are not
  sent.
- **Security**: `ISafetyStatusService.GetAsync` (spec 31), mapped to three cards (verdict, level,
  lines) plus the headline.
- Startup and security are read from Windows (registry, scheduled tasks, WMI, process list), so a
  result is cached for `WebConsoleOptions.DetailsCacheTtl` (30 s) and shared by all callers; the page
  asks every 30 s, only while the tab is visible. If a read fails it is logged, the last good value
  is served (or an "Couldn't check" / empty value when there is none) and the next request retries.

### Page

Plain DOM (`app.js`) like the rest of the console: every value from the PC (app names, publishers,
versions, product and tool names, headline) is inserted with `textContent`, never as HTML, and the
CSP still forbids inline script. The JSON serializer also escapes `<`, `>` and `&`. Status is never
colour only: icon + text for levels and impact. The nav scrolls with `scrollIntoView` rather than
`#anchor` links because the address's `#fragment` holds the access key. The page works at phone
width and follows light/dark like before.

### In-app text

The Web console page's intro now also mentions security status, updates waiting and start-up.

## Tests

- Router: each new path needs the key, is `405` for non-GET, serves camel-case JSON with enum names
  as text, and calls no source without a valid key.
- Collector with fakes: updates before/after a check, ignored count, startup sorting and no path in
  the JSON, security mapping (headline, products, remote tools incl. the "Set up by Porchlight"
  label), TTL caching with a fake clock, failure fallback.
- Encoding: values such as `<script>alert(1)</script>` and `"&` in app names, publishers and tool
  names never appear unescaped in the JSON; `app.js` never uses `innerHTML`/`outerHTML`/
  `insertAdjacentHTML`/`document.write`/`eval`.
- Tracker keeps the list; real-container composition test resolves the router.

## Acceptance criteria

- [ ] The console page shows Security, Updates waiting and Startup impact sections and a nav to them.
- [ ] No endpoint or control can change anything; `405` for any non-GET/HEAD on the new paths.
- [ ] Missing or wrong key gives `401` on the new paths.
- [ ] No `winget` run is started by a request.
- [ ] Hostile text from the PC is shown as text, not markup.
- [ ] Build has no warnings and all tests pass.
