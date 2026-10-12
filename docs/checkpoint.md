# Current checkpoint

Updated 2026-10-13. Version 1.3.0 includes the native interface refinement and
optional Mica title bar and is installed for owner testing. Its exact source,
archive and build identity are in [release readiness](release-readiness.md). This is
a source handoff, not a GitHub release announcement.

## Delivered behavior

- Mixed-DPI movement batches font/size changes before measuring commands and
  cards. Native 100%/125% crossings improved from 2.4–3.3 seconds to approximately
  0.3–0.6 seconds on the test host, with bounded layout counts and stable repeated
  dimensions. See [DPI repair evidence](cross-display-dpi.md).
- Clicking a desk-map display aligns its adjustment area and selects its brightness
  bar, or warmth when brightness is unavailable. Repeated selections realign the
  viewport. Adjustment keys then affect the selected display. Focus callbacks
  that start a scan cannot apply stale navigation or overwrite the scan status.
- The native interface uses larger display headings, shaded slate cards with
  cached surfaces, quiet help/settings commands, framed amber/neutral readouts
  and a sunken desk-map well. Primary focus and pressed feedback are distinct;
  unavailable sliders have no misleading value fill. Empty positions have no
  chip. Minimum-size and narrow-window layouts preserve wrapping and scrolling.
- The optional Windows 11 Mica title bar is off by default and activates only
  after its setting is saved. Content remains opaque; disabling clears the DWM
  request and refused persistence preserves the previous frame. This changes no
  monitor values, gamma or startup registration.
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
zero warnings and errors. The full suites pass 4,230 ControlTests and 1,765
AppTests. The navigation regression was reproduced before the fix. Focused checks
pass 236 navigation checks, 45 night-saver checks and 14 work-display dim checks.

Actual native renders were inspected for the regular and minimum-size main window,
map-selected card, narrow long-name card, 200% command layout and button states.
Rendering uses synthetic displays and temporary settings. The 64 focused DPI
checks include six native 100%/125% crossings on a private desktop and inspected
destination-DPI renders. Continuous foreground pointer dragging, physical display
control, other DPI combinations and screen-reader output remain manual acceptance.
The material-verification run's six DPI transitions took 0.48-0.63 seconds, with 19-20
command layouts and 176 card layouts per transition and stable repeated dimensions.
Source-path privacy checks pass for all three product assemblies and injected
UTF-8/UTF-16/JSON-escaped canaries. Mica tests prove DWM intent, persistence and
unchanged opaque client rendering; composited wallpaper appearance remains manual.
Hosted CI exposed an assumption that a transport write meant the rebuild's
background settings save had finished. The modal lifecycle scenario now holds
that save explicitly, verifies pending-close preservation, then verifies the
original full cleanup assertions after the confirmed save. The focused profile
lifecycle checks pass 108 assertions.

## Distribution and repository state

The source snapshot `e34bffc` produces the verified 1.3.0 Windows x64 portable
candidate in release readiness. Package and installer regressions pass. The
owner-profile GUI/CLI now match that exact payload, and Desktop/Start Menu
shortcuts target the installed GUI. Installed CLI help/version pass; all three
application JSON files kept their pre-install hashes before normal startup. The
previous installed/publish binaries, both shortcuts and JSON files were preserved
as a private local backup. No old app was running during replacement. The installed
1.3.0 window was opened, visible and responding, with no new startup error log.

The source and documentation checkpoint is published to GitHub `main`.
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

Read this page and the candidate identity in release readiness first. Use the
installed source-bound 1.3.0 candidate for owner acceptance. Inspect the
worktree and any active project lease, preserve unrelated edits and stage explicit
paths. Use the verified source-bound binaries for further acceptance. The next
work is physical display/reader acceptance and clean-machine release readiness,
then an explicitly authorized release.
Rebuild and record a new candidate if
product or packaged content changes.
