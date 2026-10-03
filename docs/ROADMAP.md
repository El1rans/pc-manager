# Roadmap

Ideas that are not built yet. Nothing here is a promise.

The previous batch (Safety page, browser hijack check, backup status, recent changes with undo,
automatic restore point, Remove apps, Printers, text size, check-up reminder and the web console's
new views) shipped in 0.3.0 as specs 31-39 in `docs/specs/`. The one before it (update history, Get
apps, Running apps, Services, startup impact and logon tasks) shipped in 0.2.0 as specs 26-30.

## Easier for the person using the PC

- **Other languages** - translate the interface, starting with Hebrew.

## Follow-ups from 0.3.0

- Verify on real PCs what was only tested with fakes: the File History configuration flag (Backup
  card), printer actions against real printers and drivers, firewall-off detection, browser policy
  reads, and a real winget removal from Remove apps.
- Check-up reminder: add a snooze. The tray balloon only supports a click, so a reminder that is
  ignored simply waits for the next period.
- Extra large text trims a few fixed-width items (the Get help address, the dashboard speed tiles).

## Follow-ups from 0.2.0

- Startup impact: check the StartupInfo reader against real data on a few PCs (it was built from a
  synthetic sample).
- Code signing: sign releases once the SignPath Foundation application is approved.
