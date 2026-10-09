# Current checkpoint

Updated 2026-10-10. This is the source and local-install handoff for version 1.2.0.
It is not a GitHub release announcement. The exact portable candidate is recorded
in [release readiness](release-readiness.md).

## Delivered behavior

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
zero warnings and errors. The full suites pass 4,225 ControlTests and 1,682
AppTests. The navigation regression was reproduced before the fix. Focused checks
pass 236 navigation checks, 45 night-saver checks and 14 work-display dim checks.

Actual native renders were inspected for the regular and minimum-size main window,
map-selected card, narrow long-name card, 200% command layout and button states.
Rendering uses synthetic displays and temporary settings. Tests do not prove
physical display behavior, actual mixed-DPI transitions or screen-reader output.

## Distribution and repository state

The 1.2.0 source snapshot is prepared for GitHub. Portable packaging, installed
binary identity, shortcut verification and hosted CI will be recorded after they
complete. No release or tag is created by this checkpoint.

## Remaining acceptance

Before publishing downloadable release assets, complete physical Night saver,
restore/hot-plug/crash recovery, real keyboard/focus, mixed DPI and screen-reader
acceptance. Clean-machine download, separate-profile install/update/uninstall and
exact-file unsigned-download/security acceptance remain separate requirements.
No independent whole-app approval or physical display pass is claimed.

## Resuming

Read this page and the candidate identity in release readiness first. Inspect the
worktree and any active project lease, preserve unrelated edits and stage explicit
paths. Use the verified source-bound binaries for further acceptance. Rebuild and
record a new candidate if product or packaged content changes.
