# Release readiness

## Current 1.4.3 candidate

Updated 2026-10-16. Glass/Mica surface recipes are distinct; content contours and
text/slider contrast are stronger. The visible Theme menu explains material
behavior. This is a source/installed-desktop checkpoint, not a published release.

| Item | Recorded value |
| --- | --- |
| Source commit | `4c6efe1ba0b6a1e5799ac5621eca4d0f99ac22ef` - 1.4.3 material fidelity checkpoint |
| GUI recorded build | `1.4.3+4c6efe1ba0b6a1e5799ac5621eca4d0f99ac22ef` |
| Package | `LumenDeck-1.4.3-win-x64.zip` |
| SHA-256 | `2d0eb2977dd2b155d807641d04ce04fda0effd0824d4b2eef531799ced4b4bc6` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.3.0` for both executables |
| Verification | Warnings-as-errors build; 2,840 AppTests; 253 material/capture checks; source-path canaries; extracted package startup; 24 installer/22 bundle checks; installed hashes/settings/shortcuts/startup verified |

Both executable product versions match the recorded build. The exact allowlist,
hashes, versions, bundled source paths, standalone CLI and synthetic GUI startup
pass with SDK/runtime lookup removed. Package regressions cover altered/extra/
version inputs, caller environment, running-app refusal and isolated installer
state. ControlTests remain unchanged and required in hosted CI; they were not
rerun locally. The actual hosted result is reported at handoff.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) checks this
record against the ZIP digest, checksum sidecar and both executable payloads.
See [checkpoint](checkpoint.md) for installation and native-evidence limits,
[packaging](packaging.md) for the contract and [reference receipt](assets/material-reference.json)
for the opaque synthetic images. The package source adds only those references
to the implementation commit. Active native Glass quality remains unverified;
partial and zero-response captures are preserved as such.

## Historical candidates

The `31af655` compact milestone was installed as 1.4.1 with matching binary hashes,
preserved settings and a verified responsive startup. The `818fa7b` Night saver
repair was prepared next. The `cc08d8a` multi-theme candidate was held after material-design feedback.
The functional material candidate was then installed as 1.4.1.
The 1.4.2 package added the visible Theme entry point and status guidance. The
1.4.3 package refines surface layering and contrast.
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
