# Release readiness

## Current 1.4.2 candidate

Updated 2026-10-16. Theme selection is directly visible in the material header,
with a shared single-level menu and explanatory material/save states. Existing
control and recovery implementations are retained. This is a source/desktop
checkpoint, not a published release.

| Item | Recorded value |
| --- | --- |
| Source commit | `64a6a8a6c93943952b399ad684c10f5c94fedbf8` - 1.4.2 visible theme checkpoint |
| GUI recorded build | `1.4.2+64a6a8a6c93943952b399ad684c10f5c94fedbf8` |
| Package | `LumenDeck-1.4.2-win-x64.zip` |
| SHA-256 | `c3aa3d9b122f43551d5aa47681fe2ca4dcc6a512845be7d2846386fbe4f3c1d9` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.2.0` for both executables |
| Verification | Release warnings-as-errors build; 2,722 AppTests; 135 material/capture checks; release-path canaries; extracted package startup; 24 installer and 22 bundle checks; installed hashes/settings/shortcuts/startup verified |

Both executable product versions match the recorded GUI build. The extracted
package passes its exact file allowlist, hashes, versions, bundled source-path
checks, standalone CLI and synthetic GUI startup with SDK/runtime lookup removed.
Package regressions cover altered/extra/version inputs, environment restoration,
running-app refusal and isolated installer-state preservation. ControlTests are
unchanged and remain required in hosted CI; this presentation update did not rerun
them locally. See the final handoff for the actual hosted result.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) verifies the
candidate record against the ZIP digest, checksum sidecar and both actual
executable payloads. See [checkpoint](checkpoint.md) for the installed desktop
outcome and [packaging](packaging.md) for the contract and limits. The
[reference receipt](assets/material-reference.json) is bound to the implementation
commit; the package source adds only those synthetic reference assets.

## Historical candidates

The `31af655` compact milestone was installed as 1.4.1 with matching binary hashes,
preserved settings and a verified responsive startup. The `818fa7b` Night saver
repair was prepared next. The `cc08d8a` multi-theme candidate was held after material-design feedback.
The functional material candidate was then installed as 1.4.1.
The current 1.4.2 package adds the visible Theme entry point and status guidance.
`ThemeId` still controls the classic panel/editors.

The `3da37c6` compact desktop build was installed as 1.4.0, with matching binary
hashes, preserved settings and verified desktop shortcuts/startup. The owner then
accepted the UI improvement and requested the 1.4.1 milestone. This candidate
superseded that package while retaining its product behavior.

Earlier 1.4.0 builds used `6a0bd4e`, `4e27b37` and `114eed4`; their evidence remains
in Git history and the [repair review](repair-review.md). Historical checks establish
only their own candidate's results. No release or release tag was created.

## Remaining release acceptance

| Area | Required evidence |
| --- | --- |
| Hardware | Real brightness/gamma acceptance, Night saver/blackout restoration, hot-plug and interrupted recovery, preserving original state |
| Native interaction | Foreground keyboard/pointer focus, continuous mixed-DPI dragging, other text sizes, real contrast themes and screen readers |
| Download/install | Clean Windows environment and separate-profile install/update/uninstall |
| Download security | Exact-file unsigned-download and antivirus acceptance; no false-positive finding is claimed |
| Review | Candidate-specific source and release-critical content review; older snapshots do not grant whole-app approval |
| Hosted CI | Actual result for the pushed revision in the [build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml) |
| Publication | Explicit approval for the intended release, tag and downloadable assets |

Successful fixture and packaging checks do not establish those manual outcomes.
Source checkpoint, portable candidate, installed app and published release remain
separate states.
