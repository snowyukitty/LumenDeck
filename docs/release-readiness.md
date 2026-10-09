# Release readiness

Updated 2026-10-10. Version 1.2.1 is a verified source checkpoint and a local
installation candidate. This page records executable evidence separately from
public release approval.

## Recorded candidate

| Item | Recorded value |
| --- | --- |
| Source commit | `12ab33e9e5b2cc0c4549b0f78473e3a36685d44f` - mixed-DPI dragging repair and regression coverage |
| GUI recorded build | `1.2.1+12ab33e9e5b2cc0c4549b0f78473e3a36685d44f` |
| Package | `LumenDeck-1.2.1-win-x64.zip` |
| SHA-256 | `f5ce096106c3196dd3e8666764a880f75adc55d5945582fe66aa686fcd733cca` |
| Distribution | Self-contained Windows x64 GUI and CLI |
| File version | `1.2.1.0` for both executables |
| Version status | Verified local candidate; desktop replacement pending old-app exit |

The recorded source is the clean snapshot used for both executables. Subsequent
checkpoint and preview updates change documentation only. Builder verification
passed on the actual ZIP. Desktop replacement is pending the running 1.2.0
app's normal Exit flow; the installer refuses to overwrite a running process.
No GitHub release or tag has been created.

## Source verification

- Release solution and both test projects: warnings-as-errors builds, zero
  warnings/errors.
- Full suites: 4,225 ControlTests and 1,746 AppTests, using synthetic displays,
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
clean-machine download behavior. The pending owner-profile update requires binary
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
