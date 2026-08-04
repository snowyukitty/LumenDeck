# Security policy

## Reporting a vulnerability

Please open a [private security advisory](https://github.com/snowyukitty/LumenDeck/security/advisories/new)
rather than a public issue.

## What this app can and cannot reach

Useful context for assessing a report:

- **It never requires administrator rights**, and does not ask for elevation.
- It writes to exactly two places: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
  (only when you switch on "Start with Windows", and only that one value), and
  `%APPDATA%\LumenDeck\`.
- It makes **no network connections at all**. There is no telemetry, no update
  check, and no external service.
- It talks to hardware through documented Win32 APIs — `dxva2.dll` for DDC/CI,
  `gdi32` for gamma ramps, and WMI for laptop panels. It installs no driver and
  no service.
- The only third-party dependency is Microsoft's `System.Management`, used for
  laptop internal-panel brightness.

## Sharing diagnostics safely

`--diagnose` output includes EDID identity and PnP device paths. Set
`LUMENDECK_ANONYMISE=1` before running it, or before taking a screenshot, and
monitor names become `Display A`, `B`, `C`.

## Supported versions

Fixes go into the next release, never back into an old tag. This is a small tool
maintained by one person, so before reporting anything, check
[Releases](https://github.com/snowyukitty/LumenDeck/releases) for a build newer
than the one you are running — `lumendeck-cli --diagnose` prints the version and
the commit it was built from.
