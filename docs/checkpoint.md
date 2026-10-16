# Current checkpoint

Updated 2026-10-16. **LumenDeck 1.5.0** is built, packaged and installed locally.
The running executable reports `1.5.0+cf0add799f8ecd78b3b6e1845bb83549ef12d630`.
This checkpoint includes source, documentation and reference images for GitHub.
No release or release tag is published.

## Delivered behavior

- Day / Evening / Night use 160 / 100 / 60 estimated nits and
  6500 / 5000 / 4000 K tint as editable starting points for different room light.
- Use this display copies the selected screen's captured brightness estimate and
  accepted tint into one mode draft. Tune the screen, open Preset targets, copy,
  review the other displays' previews, then save. Saving does not apply a mode.
- Reset this mode restores one draft without discarding other mode edits.
- Capture excludes temporary blackout/Night saver levels, unsupported controls,
  pending/refused brightness and unusable estimates. It never writes hardware.
- Both interfaces and the preset editor offer an offline Comfort guide. The
  material display menu provides a direct entry to personalization.
- Existing overrides, remembered Custom levels and hardware contrast remain
  intact. Night saver retains its reversible 5% cap and explains when to raise it.

The [research rationale](comfort-research.md) distinguishes evidence from product
choices. No source establishes these exact presets as medically optimal. Nits
and kelvin remain estimates, not measured luminance or circadian exposure.

## Verification actually completed

| Check | Result |
| --- | --- |
| Release solution, warnings as errors | Zero warnings/errors |
| Full ControlTests | 4,706 passed |
| Full AppTests, run sequentially | 2,865 passed |
| Final preset/guide capture checks | 267 passed |
| Final material integration and capture checks | 262 passed |
| Release source-path privacy | Four assemblies plus injected-path rejection checks passed |
| Portable package | Allowlist, hashes, versions, bundled paths, CLI and synthetic GUI startup passed |
| Package regressions | Extra/tampered content, wrong versions and environment restoration passed |
| Installer / bundled-path regressions | 24 state-preservation checks and 22 bundle checks passed; source validation passed |
| Installed application | Matching payload hashes; one responsive process with main window; no new error log |
| State | Three files preserved during replacement; preferences, Custom levels and shortcuts retained after startup; zero recovery records |

Public synthetic references were regenerated from committed source with an
[image receipt](assets/material-reference.json). The preset editor and material
renders were visually inspected. The native Computer Use pipe was unavailable;
startup verification used structured process/file checks. Physical light,
clinical comfort and complete native interaction acceptance remain unverified.
Hosted results for the pushed checkpoint are available in the
[build workflow](https://github.com/snowyukitty/LumenDeck/actions/workflows/build.yml).

## Installed identity and rollback

Source: `cf0add799f8ecd78b3b6e1845bb83549ef12d630`.
Package: `LumenDeck-1.5.0-win-x64.zip`.
SHA-256: `a91143ef2d2be0973bfb184675132f202f432527b20371b5f9b3c2dc27c200fc`.
Documentation-only handoff commits can follow this recorded executable source.
The previous installed files and before/at-install state snapshots are preserved
in a private local update backup, with a JSON installation receipt. Existing
Desktop and Start Menu shortcuts still resolve to the installed executable.
Normal close was attempted before authorized force termination; recovery records
were kept and checked after restart. The local project lease is released at handoff.

Scope: the LumenDeck repository and its local installed application. Test logs,
private settings and rollback copies remain outside Git. See
[release readiness](release-readiness.md) for exact package identity and remaining
manual acceptance boundaries. No further implementation is pending at this checkpoint.
