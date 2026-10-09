# Release readiness

Updated 2026-10-10. Version 1.2.0 is a verified source checkpoint and a local
installation candidate. This page records executable evidence separately from
public release approval.

## Recorded candidate

The new source snapshot is ready. The source revision, build, portable archive
and SHA-256 will be recorded here after packaging verifies the actual binaries.
No new GitHub release or tag has been created.

## Source verification

- Release solution and both test projects: warnings-as-errors builds, zero
  warnings/errors.
- Full suites: 4,225 ControlTests and 1,682 AppTests, using synthetic displays,
  fake transport and isolated settings.
- Desk-map selection: old focus behavior reproduced before repair; three window
  sizes, repeated selection, visible adjustment bars, correctly targeted adjustment
  and replacement-scan reentrancy covered.
- Night saver: 45 focused control checks and 14 actual-window work-display checks,
  including lowering a kept primary display and retaining its original brightness.
- Actual native renders inspected at regular/minimum window sizes, a selected
  card, narrow long-name card and simulated 200% command/button sizes.

These establish their tested software behavior. Physical monitors, actual
mixed-DPI transitions, foreground input and screen readers remain manual acceptance.

## Package verification contract

The builder publishes self-contained Windows x64 GUI and CLI binaries and verifies
an allowlisted ZIP containing the two EXEs, license, quickstart and payload hashes.
Checks cover binary versions, source-path privacy, standalone CLI help/version,
synthetic GUI startup and recovery from refused package/installer inputs.
[scripts/Check-ReleaseRecord.ps1](../scripts/Check-ReleaseRecord.ps1) binds the
recorded revision and digest to the archive and both actual executable payloads.
See [packaging](packaging.md) for reproducible commands and their limits.

## Remaining release acceptance

| Area | Required evidence |
| --- | --- |
| Hardware | Night saver brightness/restoration, hot-plug and interrupted recovery on a real desk |
| Native interaction | Actual keyboard/pointer focus, popups, mixed DPI/text-size transitions and screen readers |
| Download/install | Clean Windows environment and separate-profile install/update/uninstall |
| Download security | Exact-file unsigned-download and antivirus acceptance; no false-positive finding is claimed |
| Review | Final source and release-critical content assessment; no whole-app approval is carried from older snapshots |
| Hosted CI | Observe the actual run for the pushed source; local success does not prove hosted execution |
| Publication | Explicit approval for the intended release, tag and downloadable assets |

The [checkpoint](checkpoint.md) records current work and the next acceptance steps.
No release/tag, security submission, issue reply or repository visibility change
is authorized by successful engineering checks alone.
