# Release readiness

## Current 1.4.1 candidate

Updated 2026-10-16. This checkpoint adds four persistent live app themes across
the native interface while preserving the compact layout, visible Night saver
and its low work-display brightness. It is not a published release.

| Item | Recorded value |
| --- | --- |
| Source commit | `cc08d8aac9e751205c2e076fd721c789dc6df220` - 1.4.1 multi-theme checkpoint |
| GUI recorded build | `1.4.1+cc08d8aac9e751205c2e076fd721c789dc6df220` |
| Package | `LumenDeck-1.4.1-win-x64.zip` |
| SHA-256 | `acf611ffed5ae6b61f2aa75c69d170643e44f0da0c88077b74cc2aec98098bf5` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.1.0` for both executables |
| Verification | Release warnings-as-errors build; 4,610 ControlTests; 2,612 AppTests; 228 final focused theme checks; 40 committed-source showcase checks; 6 capture-export regressions; extracted package and installer/bundle regressions |

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
[public preview receipt](assets/screenshot.json) is bound to this source and uses
fictitious displays. Package probes do not change the real installation or exercise
physical monitor control.

## Historical candidates

The `31af655` compact milestone was installed as 1.4.1 with matching binary hashes,
preserved settings and a verified responsive startup. The `818fa7b` Night saver
repair was prepared next. This multi-theme candidate supersedes both portable
packages, retaining version 1.4.1 and adding the `ThemeId` preference.

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
