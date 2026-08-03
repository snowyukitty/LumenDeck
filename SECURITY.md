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

## Supported versions

The latest release. This is a small tool; fixes go forward, not into old tags.
