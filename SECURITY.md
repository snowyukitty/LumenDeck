# Security policy

## Reporting a vulnerability

Please open a [private security advisory](https://github.com/snowyukitty/LumenDeck/security/advisories/new)
rather than a public issue.

## Download blocked by security software

See [download security](docs/download-security.md) for antivirus detections,
unsigned-app prompts and information you can collect without running the app.
Do not disable protection, add an exclusion or restore a quarantined file to
work around a detection. Keep private device details and raw security logs out
of public issues. A suspected vulnerability still belongs in the private channel
above.

## What this app can and cannot reach

Useful context for assessing a report:

- **It never requires administrator rights**, and does not ask for elevation.
- Application settings, profiles, recovery state and logs live under
  `%APPDATA%\LumenDeck\`.
- The "Start with Windows" option manages the `LumenDeck` value under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` for the current account.
  Normal main-window construction can also repair an existing enabled entry
  that points to a different executable path. That repair does not enable a
  missing or empty entry. Opening Settings reads registration without repairing it.
- Self-contained downloads also extract bundled .NET native libraries to the
  runtime's extraction directory, normally under `%TEMP%\.net` on Windows.
  `DOTNET_BUNDLE_EXTRACT_BASE_DIR` can change that location; see Microsoft's
  [single-file extraction documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#native-libraries).
- It makes **no network connections at all**. There is no telemetry, no update
  check, and no external service.
- It talks to hardware through documented Win32 APIs — `dxva2.dll` for DDC/CI,
  `gdi32` for gamma ramps, and WMI for laptop panels. It installs no driver and
  no service.
- The only third-party dependency is Microsoft's `System.Management`, used for
  laptop internal-panel brightness.

## Sharing diagnostics safely

For ordinary bug reports, use Connection help > View support report or
`lumendeck-cli --support-report` when your build provides it. Review before
sharing: it excludes names, serials, device identifiers, paths and raw errors,
but counts and control ranges still describe your setup. Older releases can be
reported without this optional attachment. No report is uploaded automatically.

Detailed `--diagnose` output includes EDID identity and device identifiers.
In builds that support `LUMENDECK_ANONYMISE=1`, it replaces displayed monitor
names with `Display A`, `B`, `C`; it does not remove EDID identities, device paths
or other raw diagnostic details. Review and redact diagnostics and screenshots before sharing. Name
masking is not an anonymity guarantee.

## Supported versions

Fixes go into the next release, never back into an old tag. This is a small tool
maintained by one person, so before reporting anything, check
[Releases](https://github.com/snowyukitty/LumenDeck/releases) for a build newer
than the one you are running. To identify your build, use Settings > About
LumenDeck > Copy version if available, `lumendeck-cli --version`, or name the
release you downloaded. Detailed diagnostics are not needed just to identify a
version. The current source and local development candidates can include
features that are not in a published download.
