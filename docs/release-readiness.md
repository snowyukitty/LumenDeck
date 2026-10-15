# Release readiness

## Current 1.4.0 repair candidate

Updated 2026-10-15. This is a verified local portable candidate and source
checkpoint, not a published release or replacement of the installed application.

| Item | Recorded value |
| --- | --- |
| Source commit | `6a0bd4ed17cb8c908f119c555573ae8e99a00503` - display repairs and native-DPI capture tools |
| GUI recorded build | `1.4.0+6a0bd4ed17cb8c908f119c555573ae8e99a00503` |
| Package | `LumenDeck-1.4.0-win-x64.zip` |
| SHA-256 | `4076253b458fcea46aa7a93438a029b6973863b8191cab1175ab6fdee57d4487` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.0.0` for both executables |
| Verification | Release warnings-as-errors builds; 4,586 ControlTests; 2,227 AppTests including the hosted-size fixture correction; 30 committed-source native showcase checks; 6 capture-export regression tests |

Both executable product versions match the recorded GUI build. The extracted
package passes its exact file allowlist, hashes, versions, bundled source-path
checks, standalone CLI and synthetic GUI startup with SDK/runtime lookup removed.
Package regressions cover altered/extra/version inputs, launch-failure environment
restoration, running-app refusal and isolated installer-state preservation.
The hosted-size follow-up changes tests/documentation only; this candidate's
product source and recorded images remain unchanged.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) verifies this
single candidate record against the ZIP digest, checksum sidecar and both actual
executable payloads. Historical candidates are described separately below so the
record cannot ambiguously match multiple Package/SHA-256 rows.

See the [checkpoint](checkpoint.md) for the local artifact path, scope and evidence;
[packaging](packaging.md) defines the reproducible verification contract and limits.
The [public preview receipt](assets/screenshot.json) is bound to this source and
uses fictitious displays. Package probes never change the real installation or
exercise monitor control.

## Historical candidates

The first 1.4.0 desk-remote candidate used source `4e27b37` and the initial repair
candidate used `114eed4`. Their evidence remains in Git history and the
[repair review](repair-review.md). They are superseded by the record above.

The previously installed 1.3.0 candidate used source
`e34bffc2e57d63ed8f30ad28dbb2eae328e54c98`. Its verified installation record is retained
in [the prior readiness document](https://github.com/snowyukitty/LumenDeck/blob/1c532f5bef6b4998378f57d992896eea2d122906/docs/release-readiness.md).
That record establishes its own binary/shortcut/state checks, not acceptance of this
newer candidate. No release or release tag was created for this milestone.

## Remaining release acceptance

| Area | Required evidence |
| --- | --- |
| Hardware | Real brightness/gamma acceptance, Night saver/blackout restoration, hot-plug and interrupted recovery, with original state preserved |
| Native interaction | Foreground keyboard/pointer focus, popups, continuous mixed-DPI dragging, other text sizes, actual contrast themes and screen readers |
| Download/install | Clean Windows environment and separate-profile install/update/uninstall |
| Download security | Exact-file unsigned-download and antivirus acceptance; no false-positive finding is claimed |
| Review | Candidate-specific source and release-critical content review; older snapshots do not grant whole-app approval |
| Hosted CI | Actual result for the pushed revision in the [build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml) |
| Publication | Explicit approval for the intended release, tag and downloadable assets |

Successful fixture and packaging checks do not establish those manual outcomes.
The source checkpoint, local portable candidate, installed app and published
release remain separate states.
