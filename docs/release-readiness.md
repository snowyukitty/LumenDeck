# Release readiness

Updated 2026-10-13. Version 1.3.0 is a verified source checkpoint and a local
installation candidate. This page records executable evidence separately from
public release approval.

## Recorded candidate

| Item | Recorded value |
| --- | --- |
| Source commit | `e34bffc2e57d63ed8f30ad28dbb2eae328e54c98` - versioned native interface and Mica test candidate |
| GUI recorded build | `1.3.0+e34bffc2e57d63ed8f30ad28dbb2eae328e54c98` |
| Package | `LumenDeck-1.3.0-win-x64.zip` |
| SHA-256 | `2acc497de41bfdc3a782bf3e519ed8bf537622d10a99e296f9d89869aa34a2c1` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.3.0.0` for both executables |
| Version status | Installed local candidate; source features remain Unreleased |

The recorded source is the clean snapshot used for both executables. It includes
the material refinement, optional Mica title bar and deterministic post-modal
settings-save lifecycle checks. Builder verification passed on the actual ZIP.
No old app was running at installation. Installed GUI/CLI hashes match the exact payload and their source/build
versions match this record. Desktop and Start Menu shortcuts target the installed
GUI, CLI help/version pass, and all three application JSON files retained their
pre-install hashes before normal startup. Previous installed/publish binaries,
both shortcuts and the three JSON files were backed up privately.
The installed main window was launched and verified visible and responding with
the expected 1.3.0 build, without new error-log content. This is startup evidence,
not completed physical-display or Mica visual acceptance.
No GitHub release or tag has been created.

## Source verification

- Release solution and both test projects: warnings-as-errors builds, zero
  warnings/errors.
- Current full suites: 4,230 ControlTests and 1,767 AppTests, using synthetic displays,
  fake transport and isolated settings.
- Desk-map selection: old focus behavior reproduced before repair; three window
  sizes, repeated selection, visible adjustment bars, correctly targeted adjustment
  and replacement-scan reentrancy covered.
- Night saver: 45 focused control checks and 14 actual-window work-display checks,
  including lowering a kept primary display and retaining its original brightness.
- Actual native renders inspected at regular/minimum window sizes, a selected
  card, narrow long-name card and simulated 200% command/button sizes.
- Mixed DPI: 64 focused checks, six native 100%/125% crossings on a private
  desktop, bounded layout counts and stable dimensions. Timings improved from
  2.4–3.3 seconds to approximately 0.3–0.6 seconds; see [DPI repair](cross-display-dpi.md).
- Native material: saved/off/refused-save preference behavior, native DWM
  request readback and unchanged opaque client rendering. Physical composition
  remains owner acceptance.

These establish their tested software behavior. Physical monitor control,
foreground pointer dragging, other DPI combinations and screen readers remain
manual acceptance.

## Package verification contract

The builder publishes self-contained Windows x64 GUI and CLI binaries and verifies
an allowlisted ZIP containing the two EXEs, license, quickstart and payload hashes.
Checks cover binary versions, source-path privacy, standalone CLI help/version,
synthetic GUI startup and recovery from refused package/installer inputs.
[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) binds the
recorded revision and digest to the archive and both actual executable payloads.
See [packaging](packaging.md) for reproducible commands and their limits.

The actual candidate passed package regressions for valid/tampered/extra/version
inputs, environment restoration on injected launch failures and running-app
installer refusal. Installer source-preflight and state-preservation fixtures
also passed. These fixture results do not establish a transactional install or
clean-machine download behavior. The owner-profile update is verified by binary
hashes, shortcut targets, CLI help/version and unchanged settings/profile hashes.

## Remaining release acceptance

| Area | Required evidence |
| --- | --- |
| Hardware | Night saver brightness/restoration, hot-plug and interrupted recovery on a real desk |
| Native interaction | Actual keyboard/pointer focus, popups, continuous cross-monitor dragging, other DPI/text-size combinations and screen readers |
| Download/install | Clean Windows environment and separate-profile install/update/uninstall |
| Download security | Exact-file unsigned-download and antivirus acceptance; no false-positive finding is claimed |
| Review | Final source and release-critical content assessment; no whole-app approval is carried from older snapshots |
| Hosted CI | Check the actual [build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml) for the pushed source; local success does not prove hosted execution |
| Publication | Explicit approval for the intended release, tag and downloadable assets |

The [checkpoint](checkpoint.md) records current work and the next acceptance steps.
No release/tag, security submission, issue reply or repository visibility change
is authorized by successful engineering checks alone.
