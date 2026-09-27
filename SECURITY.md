# Security policy

## Supported versions

PC Manager is a single-user desktop app with one active line of development. Only the
[latest released version](../../releases) is supported with security fixes; there are no
long-term-support branches. Please update to the latest release before reporting an issue to
confirm it is still reproducible.

| Version | Supported |
|---|---|
| Latest release | Yes |
| Older releases | No |

## Reporting a vulnerability

Please **do not** open a public GitHub issue for a security vulnerability.

Instead, report it privately using GitHub's built-in vulnerability reporting:

1. Go to the [Security tab](../../security) of this repository.
2. Click **Report a vulnerability** to open a private advisory draft.
3. Describe the issue: affected version, steps to reproduce, and impact.

This opens a private conversation with the maintainer ([@El1rans](https://github.com/El1rans))
that is not visible to the public until a fix is released and the advisory is published. If you
cannot use GitHub's reporting flow for some reason, you may instead open a regular issue asking
the maintainer to contact you privately, without including any vulnerability details in the issue
itself.

You can expect an initial response within a few days. There is no bug bounty program.

## Scope

PC Manager runs entirely on the user's own PC. Relevant reports include (but are not limited to):

- Ways the app could be tricked into running unintended code or commands (including via winget,
  AnyDesk, OpenRGB, or PawnIO integration).
- Privilege escalation beyond what the app's admin-elevation feature already intentionally
  provides.
- Unsafe fan control behavior that could damage hardware (see `docs/specs/` for the intended
  safety design) - if you believe the safety engine can be bypassed, that is a security-relevant
  bug.
- Installer/uninstaller issues (e.g. arbitrary file/registry writes outside the documented
  locations).

Denial-of-service reports about the app crashing on malformed local input are welcome as regular
bug reports rather than security advisories, unless they demonstrate memory corruption or code
execution.
