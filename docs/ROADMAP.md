# Roadmap

Ideas that are not built yet. Nothing here is a promise.

The previous batch (update history, Get apps, Running apps, Services, startup impact and logon
tasks) shipped in 0.2.0 as specs 26-30 in `docs/specs/`.

## Is this PC safe?

- **Security status** - one card that answers "is this PC protected?": antivirus on and up to date,
  firewall on, Windows Security's own warnings (from Windows Security Center), in plain words.
- **Windows Update status** - when Windows last installed updates, whether some are waiting or
  keep failing, and a button to open Windows Update. The Updates page covers apps only today.
- **"Who can connect to this PC?"** - list remote-access tools that are installed or running
  (TeamViewer, AnyDesk, RustDesk, UltraViewer...) and flag ones the family didn't set up, a common
  sign of a phone scam.
- **Browser hijack check** - extend Browser add-ons to flag a changed home page, new-tab page or
  default search engine.

## Keeping things safe to undo

- **Backup status** - is File History or OneDrive backup on, and when did it last run? Nudge when
  nothing is being backed up.
- **Automatic restore point** - create a restore point before Porchlight runs a batch of updates or
  changes a service, if System Protection is on.
- **Recent changes** - one list of everything Porchlight changed (startup items, services, cleanup,
  installs), each with an undo where possible.

## Easier for the person using the PC

- **Remove apps** - uninstall a program from a plain list (via winget, falling back to the app's
  own uninstaller), with hints for apps that came preinstalled and are rarely used.
- **Printer fixes** - "My printer won't print": clear stuck print jobs, restart the print service,
  show which printer is the default.
- **Larger text option** - a bigger-text mode for Porchlight itself.
- **Other languages** - translate the interface, starting with Hebrew.

## For the family helper

- **Weekly check-up reminder** - a tray reminder to send the check-up report (Porchlight still
  never sends anything itself).
- **Web console: more pages** - show updates waiting, startup impact and security status (still
  read-only).

## Follow-ups from 0.2.0

- Services: let a service be set back to "Starts with Windows (delayed)".
- Startup impact: check the StartupInfo reader against real data on a few PCs (it was built from a
  synthetic sample).
- Code signing: sign releases once the SignPath Foundation application is approved.
