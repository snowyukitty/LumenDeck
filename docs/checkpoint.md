# Current checkpoint

Updated 2026-10-13. The completed milestone is the **1.4.0 desk-remote development
candidate**, prepared on `feat/desk-remote` for the `main` GitHub checkpoint.
It builds on the clean `435a62d` baseline; no unrelated changes were discarded.
The dated `feat/night-desk` and dirty `.gitignore` observations were not current.

## Delivered behavior

- Native selected-display workspace with a schematic screen selector, explicit
  outline/SELECTED marker, repeated identity and capability summary.
- Principal supported adjustment with readable exact-entry values. Unsupported
  brightness stays explained and warmth becomes principal. Estimates and gamma
  targets are explicitly distinct from measured light.
- One-action All displays mode uses the same cards. Entering a card synchronizes
  the selected screen; returning to detail keeps that screen and control.
- Spatial arrows follow the diagram, prefer aligned neighbours, stop at edges
  and never transiently focus a slider. Home/End use layout order; Enter/Space
  opens controls. Ctrl+Page Up/Down retains wrapping, control-preserving navigation.
- Selection and focus are separate accessible states. Hidden/disabled/stale items
  reject activation, hit testing resolves virtual items, and native selection/
  focus events carry the right child identity without duplicate announcements.
- Desk-wide actions stay secondary; Night saver and independent screen blackout
  keep their separate saved-state and recovery paths. Existing tray behavior stays.
- Requested, last observed, pending, matching readback, failed and unconfirmed
  values remain bound to monitor identity. WMI acceptance alone is unconfirmed.
- Native shared components reflow at compact sizes and for stacked desks. Narrow
  tile names use measured wrapping or an explicit ellipsis; the caption, selected
  control header and accessibility name retain the full identity.
- README, portable ZIP quickstart, keyboard guide, architecture/accessibility
  documentation and source-bound imagery describe the implemented application.

Final implementation/capture source: `4e27b377b8696d41d167e7a49705ca8612291df0`.
The capture source and all six image hashes are in [the receipt](assets/screenshot.json).
Documentation/artwork commits follow this immutable implementation commit.
See [design](ui-design.md), [display I/O](display-io.md) and [showcase](showcase.md).

## Verification

- Release solution and test-project builds pass with warnings treated as errors.
- Full ControlTests: **4,558** passed; full actual-window AppTests: **2,140** passed.
- Final source-bound native capture: **385** focused desk-remote checks passed.
- Fixture coverage includes 1/2/4 displays, mixed capabilities, regular/minimum/
  wide windows, stacked/offset layouts, exact entry, keyboard and accessible
  selection, in-flight writes while switching, failure/reconnect/restoration,
  reentrant focus callbacks, tray/lifecycle behavior and resource disposal.
- Native WinEvent hooks verify selected/focused child IDs. Private-desktop native
  focus plus protected key callbacks prove no intermediate slider focus.
- Simulated 100/150/200% layout and system-colour contrast checks pass. The private
  HDC remains 96 DPI: these do not establish real 150/200% monitor acceptance.
  Existing native 100/125% transitions also pass.
- Source-path privacy/canary checks pass. Self-contained GUI/CLI publish succeeds;
  extracted-package checks cover allowlisted contents, SHA-256, matching versions,
  runtime-independent CLI and synthetic GUI startup. Package negative cases,
  isolated installer-state preservation and bounded bundle-path checks pass.
- Actual native PNGs and 480x300/320x200 derivatives were inspected. The selector
  and principal value survive thumbnail reduction; no controls were repainted,
  no portrait window was stretched and no hardware state was changed for images.

## Portable candidate and publication

The local verified package is
`artifacts/milestone-1.4.0-4e27b37/package/LumenDeck-1.4.0-win-x64.zip`.
SHA-256: `0cbc46c6c3cb0dc844730baee54eee35ac162c192fc5a1eafc5a0c616b691e59`.
Both executables report `1.4.0+4e27b377b8696d41d167e7a49705ca8612291df0` with file version `1.4.0.0`.
See [release readiness](release-readiness.md) for the candidate and prior installation.

The owner requested a GitHub push. The checkpoint updates the default branch after
local verification; the existing build workflow runs on pushes and performs its
own build, synthetic tests and packaging. No workflow was weakened or disabled.
Remote CI results remain separately observable in
[GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
No release/tag is published and the previously installed 1.3.0 candidate is not
replaced. Local intermediate packages are preserved in ignored artifacts.

## Remaining acceptance and handoff

No physical brightness, gamma, power or blackout test was authorized or performed.
Real DDC/WMI/gamma, Night saver/blackout restoration, hot-plug/crash recovery and
emitted-light behavior need a concrete authorized test with initial-state preservation
and verified restoration. Narrator speech, complete UI Automation pattern coverage,
actual Windows contrast-theme switching and foreground 150/200% mixed-DPI dragging
remain manual acceptance. Fixture tests are not reported as those human checks.
Clean-machine installation/release acceptance remains separate from package probes.

Scope is this repository only; the IconFlow source study was read-only. The project
lease is released at handoff. Resume from this checkpoint and the capture receipt,
inspect Git status before changing files, and regenerate source-bound images after
any visible UI change. Preserve the distinction between source checkpoint, local
portable candidate, installed application and published release.
