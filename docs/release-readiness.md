# Release readiness

## Current 1.4.4 candidate

Updated 2026-10-16. Research-informed comfort starting points and an offline
adjustment guide are installed locally. This is a desktop/source checkpoint;
no release or release tag was published.

| Item | Recorded value |
| --- | --- |
| Source commit | `84525462dbb0ef2b3f88c05b5191bde5a422cea7` - comfort presets and guidance |
| GUI recorded build | `1.4.4+84525462dbb0ef2b3f88c05b5191bde5a422cea7` |
| Package | `LumenDeck-1.4.4-win-x64.zip` |
| SHA-256 | `343da77e1cf3bab20cadd5f2ac71f0caefd489d25e2a88485937a57c24017d13` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.4.4.0` for both executables |
| Verification | Warnings-as-errors build; 2,849 AppTests; 4,680 ControlTests before final wording trim; 235 final preset/guide checks; 262 material capture checks; package/startup and installed identities verified |

The archive passes the five-file allowlist, SHA-256, version, mapped source-path,
standalone CLI and synthetic GUI startup checks. Installed payload hashes match
its verified files. Three state files were preserved byte-for-byte during the
replacement; preferences and remembered manual levels remained intact after
startup. One responsive installed process has a main window, no startup error-log
growth and no outstanding blackout or dim recovery records.

The first full AppTests attempt hit its three-minute deadline while ControlTests
were running concurrently. A sequential rerun passed all 2,849 checks without
changing the deadline. ControlTests first caught an old Day preview expectation;
it was updated from 43% to 31% for the new 160-nit target on the 400/50 profile.
The full suite then passed. Final shorter editor wording was covered again by
235 focused checks, including narrow and scaled layouts.

Computer Use could not connect to its native pipe. Normal process close was
attempted; keep-running retained the old instance, which was force-terminated
under standing owner authorization. Existing recovery state was preserved.
Startup health is process/window identity, responsiveness and error-log evidence,
not a native screenshot or optical measurement. Synthetic guide and material
renders were visually inspected. Unchanged installer/bundle regression suites
and hosted CI were not rerun; package content checks did run.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) checks this
record against the archive and both executable payloads. See
[checkpoint](checkpoint.md) and [comfort research](comfort-research.md).

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
