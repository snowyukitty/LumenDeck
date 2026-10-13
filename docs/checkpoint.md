# Current checkpoint

Updated 2026-10-13. This checkpoint is the desk-remote development upgrade on
`feat/desk-remote`, based on the clean `main` source `435a62d`. The dated
`feat/night-desk` checkout and uncommitted `.gitignore` observation did not match
this task's starting state. No unrelated work was reset or discarded.

## Delivered behavior

- Native selected-display workspace with a keyboard-accessible schematic selector,
  explicit selection outline/marker, repeated display identity and capability summary.
- Larger principal supported adjustment; unsupported brightness stays explained and
  warmth becomes principal. Exact hardware entry and precision keys remain available.
- One-action All displays mode retains the existing repeated multi-monitor workflow.
  Stacked desks can use the shared selector at the side; compact windows reflow it above.
- Desk-wide presets, warmth restoration and Night saver are secondary. Active Night
  saver keeps recovery available; individual blackout retains separate restoration.
- Requested, last observed, pending, matching readback, failed and unconfirmed values
  remain bound to monitor identity. WMI acceptance alone never claims readback.
- Native selector accessibility and exact-entry actions; system-colour contrast palette.
  Existing tray, settings, profiles, schedule, gamma baseline and raw recovery paths stay.
- Repeatable native captures, a full-window 1200x750 portfolio image and real 480x300 /
  320x200 derivatives. README disclosure still identifies fictitious development displays.

Implementation commits are `0165fec` and `958433c`; captures are bound to `958433c`.
The source and image hashes are in [the capture receipt](assets/screenshot.json).
See [UI design](ui-design.md), [architecture](architecture.md), [display I/O](display-io.md)
and [capture procedure](showcase.md) for contracts and verification boundaries.

## Verification

The Release solution and both test projects build with warnings treated as errors.
Full ControlTests passes 4,558 checks; full AppTests passes 2,094 checks. The final
source-bound capture run passes 339 focused desk-remote checks. Tests use isolated settings, fake display
transport and private desktops; no physical brightness, gamma, power or blackout
operation was authorized or performed for this upgrade.

The fixture matrix covers 1/2/4 displays, mixed capabilities, ordinary/minimum/wide
windows, stacked orientation, keyboard selection, exact-value access, pending writes
while switching selection, failed/unconfirmed results, reconnect identity, stale
accessible items, restore access and disposal. Existing lifecycle suites cover scanning,
no displays, discovery failure, interrupted restoration, settings, CLI preflight, tray,
overlays, shortcuts and resource cleanup. Native WS_HSCROLL assertions supplement
managed scroll properties after pixel review reproduced a stale scrollbar.

100/150/200% layout evidence uses synthetic DPI messages and owned scaled fonts on a
96-DPI private HDC. It is not a claim of physical 150/200% monitor acceptance. Existing
native 100/125% crossings also pass, with no foreground activation or monitor writes.
CLI help/version and release source-path privacy/canary checks pass.

The complete native captures and the two derivatives were inspected as actual PNGs.
The selector and principal value remain understandable at 480x300; the screen shapes,
selection and adjustment hierarchy remain identifiable at 320x200. No UI was painted
into an editor and no portrait image was stretched into the landscape card.

## Distribution and remaining acceptance

This is a local source/build delivery. The earlier installed 1.3.0 candidate documented
in [release readiness](release-readiness.md) is not replaced by this task. No release,
tag, hosted deployment or remote push is created. Use the Release build adjacent to
its output DLLs for the new application; capture and tests use the same GUI assembly.

Physical hardware acceptance remains separate: real Night saver/blackout restoration,
DDC/WMI/gamma behavior, hot-plug/crash recovery and emitted-light results require a
concrete authorized test with preserved initial state and verified restoration.
Narrator announcements, actual Windows contrast-theme switching and foreground
150/200% mixed-DPI dragging remain manual acceptance. Fixture palette/automation
property tests are not reported as those human checks. Packaging/installation and
clean-machine release acceptance remain the previous separate release requirements.

## Resuming

Read this checkpoint and the capture receipt. Preserve the branch and explicit path
scope; the implementation and capture tool commits precede the documentation/artwork
commit so each image names an immutable implementation source. Rerun the capture
script after any UI change and inspect native thumbnail pixels before updating the
receipt. Work is scoped to this repository; the IconFlow source was read-only.
The project lease is released at handoff.
