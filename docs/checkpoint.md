# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.4.1** now connects the material interface to the
production controller. The owner accepted the visual direction and asked for
functional improvement. This supersedes the palette-only installation candidate
and the isolated material prototype as the next desktop update.

## Delivered behavior

- Smoke, Frost and Ceramic are persistent material finishes. Smoke/Frost support
  Mica, Glass and Solid requests, with system-policy and high-contrast fallbacks.
- Brightness, contrast, warmth, exact entry, selected-display presets and Custom
  use the existing adjustment worker and manual-state persistence.
- Night saver remains visible and retains the existing work-display dimming and
  restoration records. Desk actions contains global presets and work selection.
- Settings, preset/schedule/profile editors, blackout shortcuts, help, tray/reveal
  and normal Exit share the existing application lifecycle.
- Advanced controls opens the complete classic panel. Settings switches between
  presentations; there is one controller, not two competing display engines.
- Windows geometry uses uniform projection; values remain compact. Feedback
  distinguishes requested/observed state, and estimates track current brightness.

See [material interface](material-interface.md), [appearance](themes.md) and
[Night saver](night-saver.md). Version remains 1.4.1; no release/tag is published.

## Verification actually completed

| Check | Result |
| --- | --- |
| Release solution build, warnings as errors | Passed; zero warnings/errors |
| Full ControlTests | 4,610 passed |
| Full AppTests after integration and keyboard help | 2,676 passed |
| Committed-source material checks and reference renders | 70 passed |
| Release assembly path checks and privacy canaries | Passed; includes Material assembly |
| Extracted portable package | Allowlist, hashes, versions, bundled paths, standalone CLI and WPF/WinForms startup passed |
| Installer regressions | 24 passed |
| Bundle regressions | 22 passed; includes missing/private-path Material canaries |

The material fixtures exercise actual WPF controls with synthetic transport and
isolated files, including failed/pending saves, editor ownership, stale scans,
Custom restoration, unsupported adjustments and Night saver recovery. Six opaque
client renders and themed menus were inspected. Their [source receipt](assets/material-reference.json)
is bound to implementation commit `4e7888382c6a97cdb3a84dc368f673aa467f66d0`.
The package adds only the committed portable guide and those reference assets.

## Candidate and desktop replacement

Package source: `87a044196ecf0d6683001ecee9d1e52a03eaf034`.
Archive: `dist/build-cc1b1ad208c741e1b7b96a45b115694e/verified/LumenDeck-1.4.1-win-x64.zip`.
SHA-256: `e8d57839034f5a0da063cdbc410d79493708de4e4eea7a9401286f41a041dadc`.
Both executables report `1.4.1+87a044196ecf0d6683001ecee9d1e52a03eaf034`.
The [release record](release-readiness.md) is checked against the actual archive.

The previously installed binaries and shortcuts are backed up privately. The new
payload has been extracted and identity-checked. Normal tray Exit is required
before replacement; forced termination would bypass recovery. The installer
compares saved-state hashes before startup, verifies installed binary hashes and
shortcut targets, then checks the running version, responsive window and error-log
growth. The final handoff and ignored installation receipt record the actual
outcome; preparation alone does not establish installation.

## GitHub and remaining acceptance

The completed checkpoint is merged locally and pushed to main once, after local
verification. The final handoff reports the actual hosted result from
[GitHub Actions](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).
This push does not create a release, tag or public download asset.

The native desktop capture helper was unavailable. Reference images force an
opaque surface and do not establish DWM blur quality. Foreground keyboard/pointer
interaction, mixed-DPI movement, Narrator, real Windows contrast themes, physical
brightness/gamma/restoration, interrupted recovery and clean-profile installation
remain separate manual acceptance. No real display writes were made by fixtures.

Scope: this repository's material presentation, shared controller adapter, tests,
documentation, synthetic references and local desktop package. No sibling project
files were modified. Release the project lease at handoff. Resume from Git state,
this record and the ignored `artifacts/material-install-path.txt` installation pointer.
