# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.4.1** keeps Night saver directly accessible and
requests a much lower brightness on the retained work display. Work started from
`cfd7a93` on `fix/night-saver-entry-and-dimming`.

## Behavior

- Night saver is always first in the toolbar. Its active action is **End Night saver**;
  reserved width avoids state-change reflow. Desk actions holds the work-display
  selector and secondary commands without auto-expanding during a night session.
- The retained display requests at most 5% of its hardware brightness range,
  rounded down. An already darker display stays unchanged. Reading presets no longer
  determine this target. No software work-screen dimming or gamma change is added.
- Automatic presets and scheduled Custom preserve the retained display, including
  a display already dark on entry. Explicit manual brightness choices still take over.
- Original raw brightness is saved before dimming; restore debt, interrupted recovery
  and separately owned blackouts retain their existing protections.
- The accepted compact readouts, proportional monitor map and native scrolling remain.

Immutable product/package/capture source:
`818fa7b8ad9ee911bbf0b63dbe42c4c807c98405`.
The GUI, Core and CLI remain version 1.4.1; the settings schema is unchanged.
See [Night saver design](night-saver.md), [UI design](ui-design.md) and the
[source-bound preview receipt](assets/screenshot.json).

## Verification

| Check actually run | Result |
| --- | --- |
| Release solution build, warnings as errors | Passed, zero warnings/errors |
| Full ControlTests | 4,610 passed |
| Full AppTests | 2,377 passed |
| Committed-source native showcase | 33 passed |
| Capture exporter regressions | 6 tests passed |
| Native DPI round trips in full AppTests | 125%/150% passed existing layout-work and no-drift limits |
| Release source-path/privacy canaries | Passed |
| Extracted package and package regressions | Passed, including 24 installer and 20 bundle checks |

ControlTests and AppTests ran sequentially. Focused checks exercised low brightness,
retained snapshots, automatic schedule protection, visible start/end actions and
stable toolbar dimensions. Native compact and regular previews were visually reviewed.
All seven public previews use fictitious displays and committed source. Real settings,
display identities, backups and installation receipts remain local and ignored.
Logs are retained under ignored `artifacts/night-saver-*.log`.

## Package and desktop update

Verified archive:
`dist/build-f5933532edcf41d3bd39598081011ac6/verified/LumenDeck-1.4.1-win-x64.zip`.
SHA-256: `825fc9cc31804c71fc4b00aaff27a49557aebd0db5c5cdc28dfeba3cf1388b0e`.
Both executables report `1.4.1+818fa7b8ad9ee911bbf0b63dbe42c4c807c98405`.
[Release readiness](release-readiness.md) is the candidate identity record consumed
by `Check-ReleaseRecord.ps1`.

The previous installed 1.4.1 build (`31af655`) and shortcuts are backed up.
The replacement payload is verified. Normal tray Exit is required so restoration
and pending saves finish. The installer refuses a running app. Installation checks
compare payload hashes, shortcut targets and saved-state hashes before startup,
then verify the running path/version, responsive window and error-log growth.
The final handoff and ignored installation receipt record the actual outcome.

## GitHub and remaining acceptance

The finished checkpoint is fast-forwarded to main and pushed after local verification.
The unchanged workflow runs on every branch; only the finished main checkpoint is
pushed. The final handoff reports the exact result from
[GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
No release, tag or downloadable release asset is published by this source push.

Physical-panel darkness at the 5% hardware setting remains owner acceptance: hardware
floors vary and percentages do not measure emitted light. Physical restoration,
hot-plug/interrupted recovery, foreground mixed-DPI dragging, DWM material, Narrator,
real contrast themes and clean-profile install/uninstall remain separate acceptance.

Scope: this repository's Night saver UI/behavior, regression checks, documentation,
synthetic previews and local package/update. No sibling files or artwork were copied.
The project lease is released at handoff; resume by checking Git state, this checkpoint
and the ignored installation receipt.
