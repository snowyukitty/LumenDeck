# Release readiness

## Current 1.4.0 compact desktop candidate

Updated 2026-10-15. This verified local candidate corrects the oversized readout
and inaccurate desktop-map regressions. It is not a published release.

| Item | Recorded value |
| --- | --- |
| Source commit | `3da37c63da5824fd0b135f9338e0106e6619a96a` - compact controls and faithful desktop geometry |
| GUI recorded build | `1.4.0+3da37c63da5824fd0b135f9338e0106e6619a96a` |
| Package | `LumenDeck-1.4.0-win-x64.zip` |
| SHA-256 | `fc64e355e89e009f8cd70f4e8931603fa59230cb8f2aef5d308d4dfe0147910e` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.0.0` for both executables |
| Verification | Release warnings-as-errors builds; 4,586 ControlTests; 2,321 AppTests; 57 native screenshot checks; 33 committed-source showcase checks; 6 capture-export regressions |

Both executable product versions match the recorded GUI build. The extracted
package passes its exact file allowlist, hashes, versions, bundled source-path
checks, standalone CLI and synthetic GUI startup with SDK/runtime lookup removed.
Package regressions cover altered/extra/version inputs, launch-failure environment
restoration, running-app refusal and isolated installer-state preservation.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) verifies this
single candidate record against the ZIP digest, checksum sidecar and both actual
executable payloads. Historical candidates below do not duplicate the identity rows.

See the [checkpoint](checkpoint.md) for the local artifact and installation state;
[packaging](packaging.md) defines the verification contract and limits. The
[public preview receipt](assets/screenshot.json) is bound to this source and uses
fictitious displays. Package probes do not change the real installation or exercise
physical monitor control.

## Historical candidates

The `6a0bd4e` repair candidate was installed earlier in this session. The subsequent
owner review identified oversized percentages and inaccurate staggered screen
positions; the record above supersedes that candidate. Earlier 1.4.0 candidates
used `4e27b37` and `114eed4`; their evidence remains in Git history and the
[repair review](repair-review.md). Historical installation records establish only
their own binary, shortcut and state checks. No release or release tag was created.

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
