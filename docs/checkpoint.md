# Current checkpoint

Updated 2026-10-15. **LumenDeck 1.4.1** records the compact desktop interface
accepted after the previous installation. Work started from `58e7437` on
`chore/1.4.1-milestone-20261015`.

## Milestone

- The shared version is 1.4.1 across GUI, Core, CLI, window title, About and package.
- The accepted interface is retained: compact adjustment values, proportional
  Windows desktop geometry, spatial selection and corrected native scrolling.
- The changelog now separates the 1.4.1 checkpoint from accumulated development
  history. README, portable instructions and architecture/design notes describe
  the current interface, including number-only compact tiles and real DPI evidence.
- All seven public previews were regenerated from committed 1.4.1 source. The
  receipt binds native captures and uniform derivatives to assembly and pixel hashes.

Immutable product/package/capture source:
`31af6555d42bcfb152dd859a8af24a9c2a9a25c4`.
There are no new hardware-control behavior or settings-schema changes in this
versioning milestone. The accepted compact repairs originated in `3da37c6`.
See [UI design](ui-design.md), [capture method](showcase.md) and
[image receipt](assets/screenshot.json).

## Verification

| Check actually run on 1.4.1 | Result |
| --- | --- |
| Release solution build, warnings as errors | Passed, zero warnings/errors |
| Full ControlTests | 4,586 passed |
| Full AppTests | 2,321 passed, including the post-adjustment screenshot regression |
| Committed-source native showcase | 33 passed |
| Capture exporter regressions | 6 tests passed |
| Native DPI round trips in full AppTests | 125%/150% passed existing layout-work and no-drift limits |
| Release source-path/privacy canaries | Passed |
| Extracted package and package regressions | Passed, including installer state and compressed bundle checks |
| Source/build/archive identity record | Checked against both executable payloads and archive checksum |

ControlTests and AppTests ran sequentially. Native documentation, minimum and
stacked windows and portfolio derivatives were visually reviewed. Captures use
fictitious identities on a private desktop. The real desktop screenshot, settings,
backups and display identities are not published. These tests use fake hardware
and isolated state. Logs are retained in ignored `artifacts/milestone-1.4.1-20261015`.

## Package and desktop update

Verified archive:
`dist/build-8fb5396417774b7cbf1c33625eb20aab/verified/LumenDeck-1.4.1-win-x64.zip`.
SHA-256: `6e4aca393e9fd7e66995c163f9386906de513723e29b9266de41a56855c35220`.
Both executables report `1.4.1+31af6555d42bcfb152dd859a8af24a9c2a9a25c4`,
file version `1.4.1.0`. [Release readiness](release-readiness.md) is the single
candidate identity record consumed by `Check-ReleaseRecord.ps1`.

The accepted prior desktop build was `1.4.0+3da37c6`. Its binaries, settings and
shortcuts were backed up before preparing the new payload. Normal tray Exit is
required before replacement so restoration and pending saves finish. The installer
refuses a running app. Final installation checks compare payload hashes, shortcut
targets and saved-state hashes before startup, then verify the running path/version,
responsive window and error-log growth. The final handoff and local receipt record
the actual installation outcome; backups and receipts remain ignored.

## GitHub and remaining acceptance

The completed checkpoint is fast-forwarded to `main` and pushed after local
verification. The unchanged workflow runs on every branch and performs Windows
build, test and package checks. The final handoff reports the exact hosted result
from [GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
The source push does not create a release/tag or publish a downloadable release.

The owner accepted the preceding installed UI improvement. That is useful visual
feedback, not blanket hardware acceptance. Physical DDC/WMI/gamma restoration,
hot-plug/interrupted recovery, foreground mixed-DPI dragging, DWM material,
Narrator, real contrast-theme switching and clean-profile install/uninstall remain
separate acceptance work.

Scope: this repository's shared version, documentation, generated synthetic previews
and verified local package/desktop update. No sibling sources or reference artwork
were copied. The project lease is released at handoff; resume by checking Git state,
this checkpoint and the ignored installation receipt.
