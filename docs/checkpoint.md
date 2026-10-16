# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.4.4** is built, packaged and installed locally.
The running executable reports `1.4.4+84525462dbb0ef2b3f88c05b5191bde5a422cea7`. No release, release tag
or remote push was made for this checkpoint.

## Delivered behavior

- Day / Evening / Night use 160 / 100 / 60 estimated nits and
  6500 / 5000 / 4000 K tint as editable starting points for different room light.
- Existing overrides and remembered Custom levels remain intact. Presets leave
  hardware contrast alone. Saving targets does not apply them.
- Both interfaces and the preset editor offer an offline Comfort guide with
  practical lighting, readability, breaks, colour-work and sleep guidance.
- The editor explains mode context and unreachable estimated panel targets.
  Material preset tooltips/accessibility describe the active saved targets.
- Night saver retains its reversible 5% cap. Guidance explains that this is a
  temporary dim desk, not a reading recommendation.

The [research rationale](comfort-research.md) distinguishes evidence from product
choices. No source establishes these exact presets as medically optimal. Nits
and kelvin remain estimates, not measured luminance or circadian exposure.

## Verification actually completed

| Check | Result |
| --- | --- |
| Release solution, warnings as errors | Zero warnings/errors |
| Full AppTests, sequential rerun | 2,849 passed |
| Full ControlTests, before final wording trim | 4,680 passed |
| Final preset/guide storage and layout checks | 235 passed |
| Final material integration and capture checks | 262 passed |
| Release assembly source-path scan | Four assemblies passed |
| Portable package | Allowlist, hashes, versions, bundled paths, CLI and synthetic GUI startup passed |
| Installed application | Matching payload hashes; one responsive process with main window; no new error log |
| State | Three files preserved during replacement; preferences, Custom levels and shortcuts retained after startup; zero recovery records |

Initial failures and their resolution are recorded in [release readiness](release-readiness.md).
The native Computer Use pipe was unavailable. Startup verification used structured
process/file checks; visual inspection used synthetic renders. No physical light
measurement or claim of clinical comfort validation was made. Unchanged installer
regressions and hosted CI were not run for this task.

## Installed identity and rollback

Source: `84525462dbb0ef2b3f88c05b5191bde5a422cea7`.
Package: `LumenDeck-1.4.4-win-x64.zip`.
SHA-256: `343da77e1cf3bab20cadd5f2ac71f0caefd489d25e2a88485937a57c24017d13`.
The previous installed files and before/at-install state snapshots are preserved
in a private local update backup, with a JSON installation receipt. Existing
Desktop and Start Menu shortcuts still resolve to the installed executable.
Normal close was attempted before authorized force termination; recovery records
were kept and checked after restart. The local project lease is released at handoff.

Synthetic final screenshots and test logs remain under ignored `artifacts/`.
The public reference assets remain the earlier material-design captures.
