# Current checkpoint

Updated 2026-10-10. This is the source and local-install handoff for version 1.2.1.
It is not a GitHub release announcement. The exact portable candidate is recorded
in [release readiness](release-readiness.md).

## Delivered behavior

- Mixed-DPI movement batches font/size changes before measuring commands and
  cards. Native 100%/125% crossings improved from 2.4–3.3 seconds to approximately
  0.3–0.6 seconds on the test host, with bounded layout counts and stable repeated
  dimensions. See [DPI repair evidence](cross-display-dpi.md).
- Clicking a desk-map display aligns its adjustment area and selects its brightness
  bar, or warmth when brightness is unavailable. Repeated selections realign the
  viewport. Adjustment keys then affect the selected display. Focus callbacks
  that start a scan cannot apply stale navigation or overwrite the scan status.
- The native interface uses layered slate cards, quiet amber luminance/warmth
  controls, neutral contrast tracks, framed value readouts and desk-map brightness
  rails. Focused cards retain an amber edge and side marker. Minimum-size and
  narrow-window layouts keep their existing wrapping and scrolling behavior.
- Night saver dims the work display toward the configured Night target only when
  initially brighter, including a kept primary display. Recovery is saved before
  changing brightness. Restore, exit, Refresh and newer manual brightness choices
  preserve the documented recovery and takeover behavior.
- The current source also includes editable preset targets, the opt-in schedule,
  exact slider entry, background settings saves, offline reference windows and
  privacy-conscious support reports. See [README](../README.md) and
  [interface design](ui-design.md).

## Verification

Solution and both test projects build in Release with warnings treated as errors:
zero warnings and errors. The full suites pass 4,225 ControlTests and 1,746
AppTests. The navigation regression was reproduced before the fix. Focused checks
pass 236 navigation checks, 45 night-saver checks and 14 work-display dim checks.

Actual native renders were inspected for the regular and minimum-size main window,
map-selected card, narrow long-name card, 200% command layout and button states.
Rendering uses synthetic displays and temporary settings. The 64 focused DPI
checks include six native 100%/125% crossings on a private desktop and inspected
destination-DPI renders. Continuous foreground pointer dragging, physical display
control, other DPI combinations and screen-reader output remain manual acceptance.

## Distribution and repository state

The source snapshot `12ab33e` produces the verified 1.2.1 Windows x64 portable
candidate in release readiness. Package and installer regressions pass; previous
installed binaries and the desktop shortcut were preserved as a local backup.
Replacing the running 1.2.0 installation is pending its normal tray-menu Exit.

The completed 1.2.1 source and documentation checkpoint is published to GitHub `main`.
The private development history remains local; the public checkpoint begins
from the previous public main and includes the current implementation, tests and
maintainer-facing documentation. Inspect the actual
[build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml)
for hosted results. No release or tag is created by this checkpoint.

## Remaining acceptance

Before publishing downloadable release assets, complete physical Night saver,
restore/hot-plug/crash recovery, real keyboard/focus, continuous dragging,
other DPI combinations and screen-reader acceptance. Clean-machine download,
separate-profile install/update/uninstall and
exact-file unsigned-download/security acceptance remain separate requirements.
No independent whole-app approval or physical display pass is claimed.

## Resuming

Read this page and the candidate identity in release readiness first. Inspect the
worktree and any active project lease, preserve unrelated edits and stage explicit
paths. Use the verified source-bound binaries for further acceptance. The next
work is final desktop replacement, then physical display/reader acceptance and
clean-machine release readiness, then an explicitly authorized release.
Rebuild and record a new candidate if
product or packaged content changes.
