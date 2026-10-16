# Release readiness

## Current 1.5.0 candidate

Updated 2026-10-16. Personal comfort modes, research-informed starting points
and an offline adjustment guide are installed locally. This is a desktop/source checkpoint;
no release or release tag was published.

| Item | Recorded value |
| --- | --- |
| Source commit | `cf0add799f8ecd78b3b6e1845bb83549ef12d630` - personal comfort milestone and references |
| GUI recorded build | `1.5.0+cf0add799f8ecd78b3b6e1845bb83549ef12d630` |
| Package | `LumenDeck-1.5.0-win-x64.zip` |
| SHA-256 | `a91143ef2d2be0973bfb184675132f202f432527b20371b5f9b3c2dc27c200fc` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.5.0.0` for both executables |
| Verification | Warnings-as-errors build; 2,865 AppTests; 4,706 ControlTests; 267 preset/guide capture checks; 262 material capture checks; package regressions, startup and installed identities verified |

The archive passes the five-file allowlist, SHA-256, version, mapped source-path,
standalone CLI and synthetic GUI startup checks. Installed payload hashes match
its verified files. Three state files were preserved byte-for-byte during the
replacement; preferences and remembered manual levels remained intact after
startup. One responsive installed process has a main window, no startup error-log
growth and no outstanding blackout or dim recovery records.

The source-path guard and its injected-path rejection fixtures passed. Package
regressions rejected extra files, tampered content and wrong versions; launch
failures restored the caller environment. Installer source validation, 24
PATH/locked-file/state-preservation checks and 22 bundle/decompression checks
passed without changing real registry entries or shortcuts.

During development, the earlier 1.4.4 AppTests run hit its three-minute deadline
while ControlTests ran concurrently; sequential execution resolved it without
changing the deadline. For 1.5.0, an old layout check assumed every editor button
belonged to the pinned footer. It now checks footer visibility separately and
verifies that each new per-mode button is reachable by scrolling at simulated
200% scale. The final full suites and narrow/scaled focused captures passed.

Computer Use could not connect to its native pipe. Normal process close was
attempted; keep-running retained the old instance, which was force-terminated
under standing owner authorization. Existing recovery state was preserved.
Startup health is process/window identity, responsiveness and error-log evidence,
not a native screenshot or optical measurement. Synthetic guide and material
renders were visually inspected. The new preset reference and all material
references have source/hash receipts. Hosted CI results are recorded by the
[build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml)
for the pushed revision; local checks above do not substitute for a hosted result.

[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) checks this
record against the archive and both executable payloads. See
[checkpoint](checkpoint.md) and [comfort research](comfort-research.md).

## Historical candidates

The 1.4.4 comfort-default checkpoint was installed and verified before this
personalization milestone; its package identity remains in Git history.

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
