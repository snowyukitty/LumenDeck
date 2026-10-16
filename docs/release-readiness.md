# Release readiness

## Current 1.4.1 candidate

Updated 2026-10-16. The functional material interface shares the production
controller, settings, tray and recovery paths. Classic advanced controls remain
available. This is a source/portable checkpoint, not a published release.

| Item | Recorded value |
| --- | --- |
| Source commit | `87a044196ecf0d6683001ecee9d1e52a03eaf034` - 1.4.1 functional material checkpoint |
| GUI recorded build | `1.4.1+87a044196ecf0d6683001ecee9d1e52a03eaf034` |
| Package | `LumenDeck-1.4.1-win-x64.zip` |
| SHA-256 | `e8d57839034f5a0da063cdbc410d79493708de4e4eea7a9401286f41a041dadc` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.1.0` for both executables |
| Verification | Release warnings-as-errors build; 4,610 ControlTests; 2,678 AppTests; 70 material capture checks; release-path canaries; extracted package startup; 24 installer and 22 bundle checks |

Both executable product versions match the recorded GUI build. The extracted
package passes its exact file allowlist, hashes, versions, bundled source-path
checks, standalone CLI and synthetic GUI startup with SDK/runtime lookup removed.
Package regressions cover altered/extra/version inputs, launch-failure environment
restoration, running-app refusal and isolated installer-state preservation.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) verifies this
single candidate record against the ZIP digest, checksum sidecar and both actual
executable payloads. Historical candidates below do not duplicate the identity rows.

See the [checkpoint](checkpoint.md) for the local artifact and installation process;
[packaging](packaging.md) defines the verification contract and limits. The
[material reference receipt](assets/material-reference.json) is bound to the implementation source (the package commit adds only documentation/assets) and uses
fictitious displays. Package probes do not change the real installation or exercise
physical monitor control.

## Historical candidates

The `31af655` compact milestone was installed as 1.4.1 with matching binary hashes,
preserved settings and a verified responsive startup. The `818fa7b` Night saver
repair was prepared next. The `cc08d8a` multi-theme candidate was held after material-design feedback.
The current functional material candidate supersedes those packages while
retaining version 1.4.1. `ThemeId` still controls the classic panel/editors.

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
