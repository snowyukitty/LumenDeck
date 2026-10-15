# Current checkpoint

Updated 2026-10-15. This checkpoint corrects two regressions in the 1.4.0 desk
remote: oversized adjustment values and a selector that misrepresented staggered
Windows desktop coordinates. It starts from `cd40254` on
`fix/compact-physical-desk-20261015`.

## Delivered behavior

- Brightness, contrast and warmth share compact 30-DIP rows and the same value
  typography. Endpoint measurement preserves exact-entry readability, including
  Kelvin, without a large percentage dominating the card.
- One affine projection preserves Windows desktop offsets, adjacency, vertical
  overlap and pixel proportions. Near-aligned screens no longer occupy artificial
  grid rows. Compact tiles show numbers; the caption and card retain full identity.
- Arrow navigation follows the projected geometry, preferring overlapping neighbours.
- Observation feedback no longer leaves a stale native horizontal scrollbar. The
  card list removes only a native bar that has no managed horizontal extent,
  without resetting its vertical scroll position or hiding actual overflow.
- Compact-window review prioritizes everyday controls, readable text and disclosure
  of secondary commands. The screenshot regression now runs in the full UI suite,
  including the state after synthetic brightness, contrast and warmth requests.

The product, portable candidate and regenerated public previews use immutable source
`3da37c63da5824fd0b135f9338e0106e6619a96a`. See [UI design](ui-design.md),
[capture method](showcase.md) and [image receipt](assets/screenshot.json).
The earlier repair candidate compiled and passed its checks but did not catch these
presentation regressions; it is superseded. Passing layout checks is not a substitute
for inspecting the rendered result after interaction.

## Verification

| Check actually run | Result |
| --- | --- |
| Release solution and both test-project builds, warnings as errors | Passed, zero warnings/errors |
| Full ControlTests | 4,586 passed |
| Full AppTests, now including the screenshot regression | 2,321 passed |
| Focused screenshot layout after adjustment requests | 57 passed on native 125% and 150% displays |
| Keyboard-help follow-up after copy update | 36 passed |
| Committed-source native showcase | 33 passed |
| Capture exporter regressions | 6 tests passed |
| Native DPI round trips within full AppTests | Passed existing layout-work and no-drift limits |
| Release source-path/privacy canaries | Passed |
| Extracted package and package regressions | Passed, including installer state and compressed bundle checks |

An earlier full run timed out in the schedule-editor scenario while another test
project was running. The isolated schedule scenario passed 30 checks; subsequent
sequential full runs passed. No timeout or assertion was relaxed.

Native documentation, minimum and stacked windows and portfolio derivatives were
visually inspected. The staggered screenshot-sized fixture was inspected at 125%
and 150%, including confirmed adjustment feedback. All captures use fictitious
identities on a private desktop. The owner's screenshot and display identities
are not published. Tests use fake hardware and temporary state; no physical
brightness, gamma or Windows display configuration changes were requested by them.
Detailed logs remain in ignored `artifacts/compact-desk-20261015`.

## Portable candidate and installation

The verified local archive is
`dist/build-bc60b91a39444f01b854bce3881798f6/verified/LumenDeck-1.4.0-win-x64.zip`.
SHA-256: `fc64e355e89e009f8cd70f4e8931603fa59230cb8f2aef5d308d4dfe0147910e`.
Both executables report `1.4.0+3da37c63da5824fd0b135f9338e0106e6619a96a`,
file version `1.4.0.0`. [Release readiness](release-readiness.md) is the single
candidate identity record checked against the actual payloads.

The prior `6a0bd4e` repair build was installed earlier in this session. A backup and
verified replacement payload are prepared. Normal tray Exit is required before
replacing files; force termination bypasses restoration and state saving. The final
handoff and local installation receipt record the actual installed build, matching
binary hashes, shortcut checks and settings preservation. Local backups and receipts
remain ignored and are not packaged.

## GitHub and remaining acceptance

The owner requested a pushed checkpoint. The finished unit is fast-forwarded to
`main` after local verification. The existing workflow runs on all branches,
builds/tests on Windows and uploads a verified CI package; it does not publish a
release or deploy the installation. No workflow was weakened or disabled.
The final handoff reports the actual [GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml)
result for the pushed revision. Local success does not imply hosted success.

Physical DDC/WMI/gamma, Night saver/blackout restoration, hot-plug/interrupted
recovery and emitted-light behavior remain manual acceptance. Foreground mixed-DPI
dragging, DWM material, Narrator speech, real contrast-theme switching and a clean
Windows install/update/uninstall are not established by these fixtures.

Scope is this repository: UI implementation, regression coverage, synthetic public
previews and developer documentation. Reference design material was read only;
no private reference files or artwork were copied into the repository. The project
lease is released at handoff. Resume with Git status and this checkpoint, keeping
source, candidate, installed application and published release distinct.
